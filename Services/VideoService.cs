#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using System;
using System.Threading.Tasks;
using LivestreamRecorder.DB.Enums;
using LivestreamRecorder.DB.Exceptions;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.DTO.Video;
using LivestreamRecorderBackend.Helper;
using LivestreamRecorderBackend.Interfaces;
using LivestreamRecorderBackend.Models;
using Serilog;

namespace LivestreamRecorderBackend.Services;

public class VideoService(ILogger logger,
                          IStorageService storageService,
                          IVideoRepository videoRepository,
                          UnitOfWork_Public unitOfWorkPublic)
{
#pragma warning disable CA1859
    private readonly IUnitOfWork _unitOfWorkPublic = unitOfWorkPublic;
#pragma warning restore CA1859

    public Task<Video?> GetByVideoIdAndChannelIdAsync(string videoId, string channelId)
    {
        return videoRepository.GetVideoByIdAndChannelIdAsync(videoId, channelId);
    }

    internal async Task<Video?> AddVideoAsync(string url)
    {
        string? platform;
        if (url.Contains("youtube"))
        {
            platform = "Youtube";
        }
        else if (url.Contains("twitch"))
        {
            platform = "Twitch";
        }
        else if (url.Contains("twitcasting"))
        {
            platform = "Twitcasting";
        }
        else if (url.Contains("fc2"))
        {
            platform = "FC2";
        }
        else
        {
            logger.Warning("Unsupported platform for {url}", url);
            throw new InvalidOperationException($"Unsupported platform for {url}.");
        }

        YtdlpVideoData? info = await YoutubeDL.GetInfoByYtdlpAsync(url);
        if (null == info || string.IsNullOrEmpty(info.Id))
        {
            logger.Warning("Failed to get video info for {url}", url);
            throw new InvalidOperationException($"Failed to get video info for {url}.");
        }

        string? id = info.Id;
        string channelId = info.ChannelId ?? info.UploaderId ?? platform;
        bool isLive = info.IsLive ?? false;

        if (platform == "Twitch" && id.StartsWith('v')) id = id[1..];

        // Twitch and FC2 videos id are separate from live stream, so always set isLive to false.
        if (platform == "Twitch" || platform == "FC2") isLive = false;

        id = NameHelper.ChangeId.VideoId.DatabaseType(id, platform);
        channelId = NameHelper.ChangeId.ChannelId.DatabaseType(channelId, platform);

        if (null != await videoRepository.GetVideoByIdAndChannelIdAsync(id, channelId))
        {
            logger.Warning("Video {videoId} already exists", id);
            throw new InvalidOperationException($"Video {id} already exists.");
        }

        Video video = await videoRepository.AddOrUpdateAsync(new Video
        {
            id = id,
            Source = platform,
            Status = VideoStatus.Pending,
            SourceStatus = VideoStatus.Exist,
            IsLiveStream = isLive,
            Title = info.Title,
            Description = info.Description,
            ChannelId = channelId,
            Timestamps = new Timestamps
            {
                PublishedAt = DateTime.UtcNow
            }
        });

        _unitOfWorkPublic.Commit();

        return await videoRepository.ReloadEntityFromDBAsync(video);
    }

    internal async Task UpdateVideoAsync(Video video, UpdateVideoRequest updateVideoRequest)
    {
        await videoRepository.ReloadEntityFromDBAsync(video);

        if (updateVideoRequest.Status.HasValue) video.Status = updateVideoRequest.Status.Value;
        if (updateVideoRequest.SourceStatus.HasValue) video.SourceStatus = updateVideoRequest.SourceStatus.Value;
        if (null != updateVideoRequest.Note) video.Note = updateVideoRequest.Note;
        await videoRepository.AddOrUpdateAsync(video);
        _unitOfWorkPublic.Commit();
    }

    internal async Task RemoveVideoAsync(Video video)
    {
        await videoRepository.ReloadEntityFromDBAsync(video);
        video.Status = VideoStatus.Deleted;
        await videoRepository.AddOrUpdateAsync(video);
        _unitOfWorkPublic.Commit();
        if (null != video.Filename)
            await storageService.DeleteVideoBlobAsync(video.Filename);
    }

    /// <summary>
    ///     Get token for video.
    /// </summary>
    /// <param name="videoId"></param>
    /// <param name="channelId"></param>
    /// <returns>token</returns>
    internal async Task<string> GetTokenAsync(string videoId, string channelId)
    {
        Video? video = await videoRepository.GetVideoByIdAndChannelIdAsync(videoId, channelId);
        return null == video
            ? throw new EntityNotFoundException($"Video {video} not found")
            : await storageService.GetTokenAsync(video);
    }
}
