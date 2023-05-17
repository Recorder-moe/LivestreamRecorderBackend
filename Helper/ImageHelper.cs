using Serilog;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace LivestreamRecorderBackend.Helper;

public static class ImageHelper
{
    private static ILogger Logger => Helper.Log.Logger;
    public static async Task<string> ConvertToAvifAsync(string path)
    {
        string _ffmpegPath = "./ffmpeg.exe";

        if (File.Exists(_ffmpegPath))
        {
            _ffmpegPath = Path.GetFullPath(_ffmpegPath);
        }
        else
        {
            (string? _, string? FFmpegPath) = YoutubeDL.WhereIs();
            _ffmpegPath = FFmpegPath ?? throw new ConfigurationErrorsException("FFmpeg is missing.");
        }

        FFmpeg.SetExecutablesPath(Path.GetDirectoryName(_ffmpegPath), "ffmpeg.exe", "ffprobe.exe");

        IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(path);
        var outputPath = Path.ChangeExtension(path, ".avif");

        IConversion conversion = FFmpeg.Conversions.New()
                                   .AddStream(mediaInfo.Streams)
                                   .AddParameter($"-c:v libaom-av1 -still-picture 1")
                                   .SetOutput(outputPath)
                                   .SetOverwriteOutput(true);
        conversion.OnProgress += (_, e)
            => Logger.Verbose("Progress: {progress}%", e.Percent);
        conversion.OnDataReceived += (_, e)
            => Logger.Verbose(e.Data ?? "");
        Logger.Debug("FFmpeg arguments: {arguments}", conversion.Build());

        IConversionResult convRes = await conversion.Start();

        return outputPath;
    }
}
