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
    private readonly IUnitOfWork _unitOfWork_Public;
    private readonly IVideoRepository _videoRepository;
    private readonly ILogger _logger;
    private readonly IStorageService _storageService;

    public VideoService(
        ILogger logger,
        IStorageService storageService,
        IVideoRepository videoRepository,
        UnitOfWork_Public unitOfWork_Public)
    {
        _logger = logger;
        _storageService = storageService;
        _videoRepository = videoRepository;
        _unitOfWork_Public = unitOfWork_Public;
    }

    public Task<Video?> GetByVideoIdAndChannelIdAsync(string videoId, string channelId)
        => _videoRepository.GetVideoByIdAndChannelIdAsync(videoId, channelId);

    internal async Task<Video?> AddVideoAsync(string url)
    {
        string? Platform;
        if (url.Contains("youtube"))
        {
            Platform = "Youtube";
        }
        else if (url.Contains("twitch"))
        {
            Platform = "Twitch";
        }
        else if (url.Contains("twitcasting"))
        {
            Platform = "Twitcasting";
        }
        else if (url.Contains("fc2"))
        {
            Platform = "FC2";
        }
        else
        {
            _logger.Warning("Unsupported platform for {url}", url);
            throw new InvalidOperationException($"Unsupported platform for {url}.");
        }

        var info = await Helper.YoutubeDL.GetInfoByYtdlpAsync(url);
        if (null == info || string.IsNullOrEmpty(info.Id))
        {
            _logger.Warning("Failed to get video info for {url}", url);
            throw new InvalidOperationException($"Failed to get video info for {url}.");
        }

        var id = info.Id;
        string channelId = info.ChannelId ?? info.UploaderId ?? Platform;
        var islive = info.IsLive ?? false;

        if (Platform == "Twitch" && id.StartsWith("v"))
        {
            id = id[1..];
        }

        // Twitch and FC2 videos id are seperate from live stream, so always set islive to false.
        if (Platform == "Twitch" || Platform == "FC2")
        {
            islive = false;
        }

        id = NameHelper.ChangeId.VideoId.DatabaseType(id, Platform);
        channelId = NameHelper.ChangeId.ChannelId.DatabaseType(channelId, Platform);

        if (null != await _videoRepository.GetVideoByIdAndChannelIdAsync(id, channelId))
        {
            _logger.Warning("Video {videoId} already exists", id);
            throw new InvalidOperationException($"Video {id} already exists.");
        }

        var video = await _videoRepository.AddOrUpdateAsync(new()
        {
            id = id,
            Source = Platform,
            Status = VideoStatus.Pending,
            SourceStatus = VideoStatus.Exist,
            IsLiveStream = islive,
            Title = info.Title,
            Description = info.Description,
            ChannelId = channelId,
            Timestamps = new Timestamps()
            {
                PublishedAt = DateTime.UtcNow,
            },
        });
        _unitOfWork_Public.Commit();

        return await _videoRepository.ReloadEntityFromDBAsync(video);
    }

    internal async Task UpdateVideoAsync(Video video, UpdateVideoRequest updateVideoRequest)
    {
        await _videoRepository.ReloadEntityFromDBAsync(video);

        if (updateVideoRequest.Status.HasValue) video.Status = updateVideoRequest.Status.Value;
        if (updateVideoRequest.SourceStatus.HasValue) video.SourceStatus = updateVideoRequest.SourceStatus.Value;
        if (null != updateVideoRequest.Note) video.Note = updateVideoRequest.Note;
        await _videoRepository.AddOrUpdateAsync(video);
        _unitOfWork_Public.Commit();
    }

    internal async Task RemoveVideoAsync(Video video)
    {
        await _videoRepository.ReloadEntityFromDBAsync(video);
        video.Status = VideoStatus.Deleted;
        await _videoRepository.AddOrUpdateAsync(video);
        _unitOfWork_Public.Commit();
        if (null != video.Filename)
            await _storageService.DeleteVideoBlobAsync(video.Filename);
    }

    /// <summary>
    /// Get token for video.
    /// </summary>
    /// <param name="videoId"></param>
    /// <returns>token</returns>
    internal async Task<string> GetTokenAsync(string videoId, string channelId)
    {
        Video? video = await _videoRepository.GetVideoByIdAndChannelIdAsync(videoId, channelId);
        return null == video
            ? throw new EntityNotFoundException($"Video {video} not found")
            : await _storageService.GetTokenAsync(video);
    }
}
