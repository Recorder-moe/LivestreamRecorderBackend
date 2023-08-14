﻿#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using LivestreamRecorder.DB.Enums;
using LivestreamRecorder.DB.Exceptions;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.DTO.Video;
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

    public Task<Video?> GetByVideoIdAndChannelId(string videoId, string channelId)
        => _videoRepository.GetByVideoIdAndChannelId(videoId, channelId);

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

        // Youtube video id may start with '_' which is not allowed in CouchDB.
        // So we add a prefix 'Y' to it.
        if (Platform == "Youtube")
        {
            id = "Y" + id;
        }

        if (null != await _videoRepository.GetByVideoIdAndChannelId(id, channelId))
        {
            _logger.Warning("Video {videoId} already exists", id);
            throw new InvalidOperationException($"Video {id} already exists.");
        }

        var video = await _videoRepository.AddOrUpdate(new()
        {
            id = id,
            Source = Platform,
            Status = VideoStatus.Pending,
            SourceStatus = VideoStatus.Exist,
            Title = info.Title,
            Description = info.Description,
            ChannelId = channelId,
            Timestamps = new Timestamps()
            {
                PublishedAt = DateTime.UtcNow,
            },
        });
        _unitOfWork_Public.Commit();

        return await _videoRepository.ReloadEntityFromDB(video);
    }

    internal async Task UpdateVideoAsync(Video video, UpdateVideoRequest updateVideoRequest)
    {
        await _videoRepository.ReloadEntityFromDB(video);

        if (updateVideoRequest.Status.HasValue) video.Status = updateVideoRequest.Status.Value;
        if (updateVideoRequest.SourceStatus.HasValue) video.SourceStatus = updateVideoRequest.SourceStatus.Value;
        if (null != updateVideoRequest.Note) video.Note = updateVideoRequest.Note;
        await _videoRepository.AddOrUpdate(video);
        _unitOfWork_Public.Commit();
    }

    internal async Task RemoveVideoAsync(Video video)
    {
        await _videoRepository.ReloadEntityFromDB(video);
        video.Status = VideoStatus.Deleted;
        await _videoRepository.AddOrUpdate(video);
        _unitOfWork_Public.Commit();
        if (null != video.Filename)
            await _storageService.DeleteVideoBlob(video.Filename);
    }

    /// <summary>
    /// Get token for video.
    /// </summary>
    /// <param name="videoId"></param>
    /// <returns>token</returns>
    internal async Task<string> GetToken(string videoId, string channelId)
    {
        Video? video = await _videoRepository.GetByVideoIdAndChannelId(videoId, channelId);
        return null == video
            ? throw new EntityNotFoundException($"Video {video} not found")
            : await _storageService.GetToken(video);
    }
}
