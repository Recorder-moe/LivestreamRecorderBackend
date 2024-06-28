﻿using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LivestreamRecorderBackend.Helper;
using LivestreamRecorderBackend.Models;
using Serilog;

namespace LivestreamRecorderBackend.Services.PlatformService;

public class YoutubeService(ILogger logger)
{
    public async Task<(string? avatarUrl, string? bannerUrl, string? channelName)> GetChannelData(
        string channelId,
        CancellationToken cancellation)
    {
        YtdlpVideoData._YtdlpVideoData? info =
            await YoutubeDL.GetInfoByYtdlpAsync($"https://www.youtube.com/channel/{channelId}", cancellation);

        if (null == info)
        {
            logger.Warning("Failed to get channel info for {channelId}", channelId);
            return (null, null, null);
        }

        string? channelName = info.Uploader;

        var thumbnails = info.Thumbnails.OrderByDescending(p => p.Preference).ToList();
        string? avatarUrl = thumbnails.FirstOrDefault()?.Url;
        string? bannerUrl = thumbnails.Skip(1).FirstOrDefault()?.Url;
        return (avatarUrl, bannerUrl, channelName);
    }
}