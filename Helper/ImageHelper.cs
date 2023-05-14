using Serilog;
using System.IO;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace LivestreamRecorderService.Helper;

public static class ImageHelper
{
    public static async Task<string> ConvertToAvifAsync(string path)
    {
        string _ffmpegPath = "./ffmpeg.exe";

        if (File.Exists(_ffmpegPath))
        {
            _ffmpegPath = Path.GetFullPath(_ffmpegPath);
        }

        FFmpeg.SetExecutablesPath(_ffmpegPath);

        IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(path);
        var outputPath = Path.ChangeExtension(path, ".avif");

        IConversion conversion = FFmpeg.Conversions.New()
                                   .AddStream(mediaInfo.Streams)
                                   .AddParameter($"-c:v libaom-av1 -still-picture 1")
                                   .SetOutput(outputPath)
                                   .SetOverwriteOutput(true);
        conversion.OnProgress += (_, e)
            => Log.Verbose("Progress: {progress}%", e.Percent);
        conversion.OnDataReceived += (_, e)
            => Log.Verbose(e.Data ?? "");
        Log.Debug("FFmpeg arguments: {arguments}", conversion.Build());

        IConversionResult convRes = await conversion.Start();

        return outputPath;
    }
}
