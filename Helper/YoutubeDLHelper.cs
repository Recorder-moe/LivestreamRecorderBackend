﻿using LivestreamRecorderBackend.Models;
using Serilog;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using YoutubeDLSharp;
using YoutubeDLSharp.Helpers;
using YoutubeDLSharp.Options;

namespace LivestreamRecorderBackend.Helper;

internal static partial class YoutubeDL
{
    private static ILogger Logger => Helper.Log.Logger;
#nullable disable
    /// <summary>
    /// Modified from YoutubeDL.RunVideoDataFetch()
    /// </summary>
    /// <param name="ytdl"></param>
    /// <param name="url"></param>
    /// <param name="ct"></param>
    /// <param name="flat"></param>
    /// <param name="overrideOptions"></param>
    /// <returns></returns>
#pragma warning disable CA1068 // CancellationToken 參數必須位於最後
    public static async Task<RunResult<YtdlpVideoData>> RunVideoDataFetch_Alt(this YoutubeDLSharp.YoutubeDL ytdl, string url, CancellationToken ct = default, bool flat = true, OptionSet overrideOptions = null)
#pragma warning restore CA1068 // CancellationToken 參數必須位於最後
    {
        OptionSet optionSet = new()
        {
            IgnoreErrors = ytdl.IgnoreDownloadErrors,
            IgnoreConfig = true,
            NoPlaylist = true,
            HlsPreferNative = true,
            ExternalDownloaderArgs = "-nostats -loglevel 0",
            Output = Path.Combine(ytdl.OutputFolder, ytdl.OutputFileTemplate),
            RestrictFilenames = ytdl.RestrictFilenames,
            NoContinue = ytdl.OverwriteFiles,
            NoOverwrites = !ytdl.OverwriteFiles,
            NoPart = true,
            FfmpegLocation = Utils.GetFullPath(ytdl.FFmpegPath),
            Exec = "echo {}"
        };
        if (overrideOptions != null)
        {
            optionSet = optionSet.OverrideOptions(overrideOptions);
        }

        optionSet.DumpSingleJson = true;
        optionSet.FlatPlaylist = flat;
        YtdlpVideoData videoData = null;
        YoutubeDLProcess youtubeDLProcess = new(ytdl.YoutubeDLPath);
        youtubeDLProcess.OutputReceived += (o, e) =>
        {
            // Workaround: Fix invalid json directly
            var data = e.Data.Replace("\"[{", "[{")
                             .Replace("}]\"", "}]")
                             .Replace("False", "false")
                             .Replace("True", "true");
            // Change json string from 'sth' to "sth"
            data = new Regex("(?:[\\s:\\[\\{\\(])'([^'\\r\\n\\s]*)'(?:\\s,]}\\))").Replace(data, @"""$1""");
            videoData = Newtonsoft.Json.JsonConvert.DeserializeObject<YtdlpVideoData>(data);
        };
        FieldInfo fieldInfo = typeof(YoutubeDLSharp.YoutubeDL).GetField("runner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.SetField);
        (int code, string[] errors) = await (fieldInfo.GetValue(ytdl) as ProcessRunner).RunThrottled(youtubeDLProcess, new[] { url }, optionSet, ct);
        return new RunResult<YtdlpVideoData>(code == 0, errors, videoData);
    }
#nullable enable 

    /// <summary>
    /// 尋找程式路徑
    /// </summary>
    /// <returns>Full path of yt-dlp and FFmpeg</returns>
    /// <exception cref="BadImageFormatException" >The function is only works in windows.</exception>
    public static (string? YtdlPath, string? FFmpegPath) WhereIs()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new BadImageFormatException("The WhereIs yt-dlp, ffmpeg method is olny works in Windows!");

        DirectoryInfo TempDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), nameof(LivestreamRecorderBackend)));

        // https://stackoverflow.com/a/63021455
        string[] paths = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? Array.Empty<string>();
        string[] extensions = Environment.GetEnvironmentVariable("PATHEXT")?.Split(';') ?? Array.Empty<string>();

        string? _YtdlpPath = (from p in new[]
                                {
                                    Environment.CurrentDirectory,
                                    TempDirectory.FullName,
                                    @"C:\home\site\wwwroot"
                                }.Concat(paths)
                              from e in extensions
                              let path = Path.Combine(p.Trim(), "yt-dlp" + e.ToLower())
                              where File.Exists(path)
                              select path)?.FirstOrDefault();
        string? _FFmpegPath = (from p in new[]
                                {
                                    Environment.CurrentDirectory,
                                    TempDirectory.FullName,
                                    @"C:\home\site\wwwroot"
                                }.Concat(paths)
                               from e in extensions
                               let path = Path.Combine(p.Trim(), "ffmpeg" + e.ToLower())
                               where File.Exists(path)
                               select path)?.FirstOrDefault();

        if (string.IsNullOrEmpty(_YtdlpPath))
        {
            Logger.Fatal("Cannot found yt-dlp.exe");
        }
        else
        {
            Logger.Debug("Found yt-dlp.exe at {YtdlpPath}", _YtdlpPath);
        }

        if (string.IsNullOrEmpty(_FFmpegPath))
        {
            Logger.Fatal("Cannot found ffmpeg.exe");
        }
        else
        {
            Logger.Debug("Found ffmpeg.exe at {FFmpegPath}", _FFmpegPath);
        }

        return (_YtdlpPath, _FFmpegPath);
    }

    public static async Task<YtdlpVideoData?> GetInfoByYtdlpAsync(string url, CancellationToken cancellation = default)
    {
        string _ytdlPath = "./yt-dlp.exe";
        string _ffmpegPath = "./ffmpeg.exe";

        if (File.Exists(_ytdlPath) && File.Exists(_ffmpegPath))
        {
            _ytdlPath = Path.GetFullPath(_ytdlPath);
            _ffmpegPath = Path.GetFullPath(_ffmpegPath);
        }
        else
        {
            (string? YtdlPath, string? FFmpegPath) = WhereIs();
            _ytdlPath = YtdlPath ?? throw new ConfigurationErrorsException("Yt-dlp is missing.");
            _ffmpegPath = FFmpegPath ?? throw new ConfigurationErrorsException("FFmpeg is missing.");
        }

        var ytdl = new YoutubeDLSharp.YoutubeDL
        {
            YoutubeDLPath = _ytdlPath,
            FFmpegPath = _ffmpegPath
        };

        OptionSet optionSet = new();
        optionSet.AddCustomOption("--ignore-no-formats-error", true);

        try
        {
            var res = await ytdl.RunVideoDataFetch_Alt(url, overrideOptions: optionSet, ct: cancellation);
            if (!res.Success)
            {
                throw new Exception(string.Join(' ', res.ErrorOutput));
            }

            YtdlpVideoData videoData = res.Data;
            return videoData;
        }
        catch (Exception e)
        {
            Logger.Error(e, "An exception occurred while getting info by yt-dlp: {url}", url);
            return null;
        }
    }
}