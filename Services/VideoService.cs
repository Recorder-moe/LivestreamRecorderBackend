using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Enums;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.DTO.Video;
using Serilog;
using System;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Services;

public class VideoService
{
    private readonly IUnitOfWork _unitOfWork_Public;
    private readonly IVideoRepository _videoRepository;
    private readonly ILogger _logger;
    private readonly ABSService _aBSservice;

    public VideoService(
        ILogger logger,
        ABSService aBSservice,
        IVideoRepository videoRepository,
        UnitOfWork_Public unitOfWork_Public)
    {
        _logger = logger;
        _aBSservice = aBSservice;
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

    internal void RemoveVideo(Video video)
    {
        video.Status = VideoStatus.Deleted;
        _videoRepository.Update(video);
        _unitOfWork_Public.Commit();
        var blobClient = _aBSservice.GetVideoBlob(video);
        blobClient.DeleteIfExists();
    }

    /// <summary>
    /// Get SAS token for video.
    /// </summary>
    /// <param name="videoId"></param>
    /// <param name="blobContainerClient"></param>
    /// <returns>SAS uri</returns>
    internal async Task<string?> GetSASTokenAsync(string videoId)
    {
        var video = GetVideoById(videoId);
        var blobClient = _aBSservice.GetVideoBlob(video);
        return null != blobClient
                   && await blobClient.ExistsAsync()
                   && blobClient.CanGenerateSasUri
               ? blobClient.GenerateSasUri(Azure.Storage.Sas.BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(12)).Query
               : null;
    }
}
