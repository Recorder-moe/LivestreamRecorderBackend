#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using LivestreamRecorder.DB.Enums;
using LivestreamRecorder.DB.Exceptions;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.DTO.Video;
using LivestreamRecorderBackend.Helper;
using LivestreamRecorderBackend.Interfaces;
using Serilog;
using System;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Services;

public class VideoService
{
    private readonly IUnitOfWork _unitOfWorkPublic;
    private readonly IVideoRepository _videoRepository;
    private readonly ILogger _logger;
    private readonly IStorageService _storageService;

    public VideoService(
        ILogger logger,
        IStorageService storageService,
        IVideoRepository videoRepository,
        // ReSharper disable once SuggestBaseTypeForParameterInConstructor
        UnitOfWork_Public unitOfWorkPublic)
    {
        _logger = logger;
        _storageService = storageService;
        _videoRepository = videoRepository;
        _unitOfWorkPublic = unitOfWorkPublic;
    }

    public Task<Video?> GetByVideoIdAndChannelIdAsync(string videoId, string channelId)
        => _videoRepository.GetVideoByIdAndChannelIdAsync(videoId, channelId);

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
            _logger.Warning("Unsupported platform for {url}", url);
            throw new InvalidOperationException($"Unsupported platform for {url}.");
        }

        var info = await YoutubeDL.GetInfoByYtdlpAsync(url);
        if (null == info || string.IsNullOrEmpty(info.Id))
        {
            _logger.Warning("Failed to get video info for {url}", url);
            throw new InvalidOperationException($"Failed to get video info for {url}.");
        }

        var id = info.Id;
        var channelId = info.ChannelId ?? info.UploaderId ?? platform;
        var isLive = info.IsLive ?? false;

        if (platform == "Twitch" && id.StartsWith("v"))
        {
            id = id[1..];
        }

        // Twitch and FC2 videos id are separate from live stream, so always set isLive to false.
        if (platform == "Twitch" || platform == "FC2")
        {
            isLive = false;
        }

        id = NameHelper.ChangeId.VideoId.DatabaseType(id, platform);
        channelId = NameHelper.ChangeId.ChannelId.DatabaseType(channelId, platform);

        if (null != await _videoRepository.GetVideoByIdAndChannelIdAsync(id, channelId))
        {
            _logger.Warning("Video {videoId} already exists", id);
            throw new InvalidOperationException($"Video {id} already exists.");
        }

        var video = await _videoRepository.AddOrUpdateAsync(new()
        {
            id = id,
            Source = platform,
            Status = VideoStatus.Pending,
            SourceStatus = VideoStatus.Exist,
            IsLiveStream = isLive,
            Title = info.Title,
            Description = info.Description,
            ChannelId = channelId,
            Timestamps = new Timestamps()
            {
                PublishedAt = DateTime.UtcNow,
            },
        });

        _unitOfWorkPublic.Commit();

        return await _videoRepository.ReloadEntityFromDBAsync(video);
    }

    internal async Task UpdateVideoAsync(Video video, UpdateVideoRequest updateVideoRequest)
    {
        await _videoRepository.ReloadEntityFromDBAsync(video);

        if (updateVideoRequest.Status.HasValue) video.Status = updateVideoRequest.Status.Value;
        if (updateVideoRequest.SourceStatus.HasValue) video.SourceStatus = updateVideoRequest.SourceStatus.Value;
        if (null != updateVideoRequest.Note) video.Note = updateVideoRequest.Note;
        await _videoRepository.AddOrUpdateAsync(video);
        _unitOfWorkPublic.Commit();
    }

    internal async Task RemoveVideoAsync(Video video)
    {
        await _videoRepository.ReloadEntityFromDBAsync(video);
        video.Status = VideoStatus.Deleted;
        await _videoRepository.AddOrUpdateAsync(video);
        _unitOfWorkPublic.Commit();
        if (null != video.Filename)
            await _storageService.DeleteVideoBlobAsync(video.Filename);
    }

    /// <summary>
    /// Get token for video.
    /// </summary>
    /// <param name="videoId"></param>
    /// <param name="channelId"></param>
    /// <returns>token</returns>
    internal async Task<string> GetTokenAsync(string videoId, string channelId)
    {
        var video = await _videoRepository.GetVideoByIdAndChannelIdAsync(videoId, channelId);
        return null == video
            ? throw new EntityNotFoundException($"Video {video} not found")
            : await _storageService.GetTokenAsync(video);
    }
}
