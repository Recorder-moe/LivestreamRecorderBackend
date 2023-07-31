using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Enums;
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

    internal bool IsVideoArchived(string videoId)
    {
        var video = _videoRepository.GetById(videoId);
        // Check if video is archived
        return video.Status == VideoStatus.Archived
               || video.Size.HasValue
               || video.Size > 0;
    }

    internal Video GetVideoById(string id) => _videoRepository.GetById(id);

    internal async Task<string> AddVideoAsync(string url)
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
        if (_videoRepository.Exists(id))
        {
            _logger.Warning("Video {videoId} already exists", id);
            throw new InvalidOperationException($"Video {id} already exists.");
        }

        _videoRepository.Add(new()
        {
            id = id,
            Source = Platform,
            Status = VideoStatus.Pending,
            SourceStatus = VideoStatus.Exist,
            Title = info.Title,
            Description = info.Description,
            ChannelId = info.ChannelId ?? info.UploaderId ?? Platform,
            Timestamps = new Timestamps()
            {
                PublishedAt = DateTime.UtcNow,
            },
        });
        _unitOfWork_Public.Commit();

        return id;
    }

    internal void UpdateVideo(Video video, UpdateVideoRequest updateVideoRequest)
    {
        var v = _videoRepository.GetById(video.id);
        if (updateVideoRequest.Status.HasValue) v.Status = updateVideoRequest.Status.Value;
        if (updateVideoRequest.SourceStatus.HasValue) v.SourceStatus = updateVideoRequest.SourceStatus.Value;
        if (null != updateVideoRequest.Note) v.Note = updateVideoRequest.Note;
        _videoRepository.Update(v);
        _unitOfWork_Public.Commit();
    }

    internal async Task RemoveVideoAsync(Video video)
    {
        video.Status = VideoStatus.Deleted;
        _videoRepository.Update(video);
        _unitOfWork_Public.Commit();
        if (null != video.Filename)
            await _storageService.DeleteVideoBlob(video.Filename);
    }

    /// <summary>
    /// Get token for video.
    /// </summary>
    /// <param name="videoId"></param>
    /// <returns>token</returns>
    internal Task<string> GetToken(string videoId)
        => _storageService.GetToken(GetVideoById(videoId));
}
