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
        IMediaInfo? mediaInfo = await FFmpeg.GetMediaInfo(path);

        if (mediaInfo == null)
        {
            Logger.Error("Failed to get media info for {path}", path);
            throw new FileNotFoundException("Media info not found", path);
        }
        else
        {
            Logger.Verbose("Media info for {path}: {mediaInfo}", path, mediaInfo);
        }

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
