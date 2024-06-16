using System.Configuration;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Serilog;
using Xabe.FFmpeg;

namespace LivestreamRecorderBackend.Helper;

public static class ImageHelper
{
    private static ILogger Logger => Log.Logger;

    public static async Task<string> ConvertToAvifAsync(string path)
    {
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        string extension = isWindows ? ".exe" : "";
        string ffmpegPath = "./ffmpeg" + extension;

        if (File.Exists(ffmpegPath))
        {
            ffmpegPath = Path.GetFullPath(ffmpegPath);
        }
        else
        {
            (_, string? fFmpegPath) = YoutubeDL.WhereIs();
            ffmpegPath = fFmpegPath ?? throw new ConfigurationErrorsException("FFmpeg is missing.");
        }

        FFmpeg.SetExecutablesPath(Path.GetDirectoryName(ffmpegPath), "ffmpeg" + extension, "ffprobe" + extension);

        IMediaInfo? mediaInfo = await FFmpeg.GetMediaInfo(path);
        string outputPath = Path.ChangeExtension(path, ".avif");

        IConversion? conversion = FFmpeg.Conversions.New()
                                        .AddStream(mediaInfo.Streams)
                                        .AddParameter("-c:v libaom-av1 -still-picture 1")
                                        .SetOutput(outputPath)
                                        .SetOverwriteOutput(true);

        conversion.OnProgress += (_, e)
            => Logger.Verbose("Progress: {progress}%", e.Percent);

        conversion.OnDataReceived += (_, e)
            => Logger.Verbose(e.Data ?? "");

        Logger.Debug("FFmpeg arguments: {arguments}", conversion.Build());

        await conversion.Start();

        return outputPath;
    }
}
