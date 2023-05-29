using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Enum;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.DTO.Video;
using Serilog;
using System;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Services;

internal class VideoService : IDisposable
{
    private static ILogger Logger => Helper.Log.Logger;
    private bool _disposedValue;
    private readonly IUnitOfWork _publicUnitOfWork;
    private readonly VideoRepository _videoRepository;
    private readonly ABSservice _aBSService;

    public VideoService()
    {
        (_, _publicUnitOfWork) = Helper.Database.MakeDBContext<PublicContext, UnitOfWork_Public>();
        _videoRepository = new VideoRepository((UnitOfWork_Public)_publicUnitOfWork);

        _aBSService = new ABSservice();
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
        else
        {
            Logger.Warning("Unsupported platform for {url}", url);
            throw new InvalidOperationException($"Unsupported platform for {url}.");
        }

        var info = await Helper.YoutubeDL.GetInfoByYtdlpAsync(url);
        if (null == info || string.IsNullOrEmpty(info.Id))
        {
            Logger.Warning("Failed to get video info for {url}", url);
            throw new InvalidOperationException($"Failed to get video info for {url}.");
        }

        var id = info.Id;
        if (_videoRepository.Exists(id))
        {
            Logger.Warning("Video {videoId} already exists", id);
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
            ChannelId = info.ChannelId ?? info.UploaderId,
            Timestamps = new Timestamps()
            {
                PublishedAt = DateTime.UtcNow,
            },
        });
        _publicUnitOfWork.Commit();

        return id;
    }

    internal void UpdateVideo(Video video, UpdateVideoRequest updateVideoRequest)
    {
        var v = _videoRepository.GetById(video.id);
        if (null != updateVideoRequest.Status) v.Status = updateVideoRequest.Status.Value;
        if (null != updateVideoRequest.SourceStatus) v.SourceStatus = updateVideoRequest.SourceStatus.Value;
        if (null != updateVideoRequest.Note) v.Note = updateVideoRequest.Note;
        _videoRepository.Update(v);
        _publicUnitOfWork.Commit();
    }

    internal void RemoveVideo(Video video)
    {
        video.Status = VideoStatus.Deleted;
        _videoRepository.Update(video);
        _publicUnitOfWork.Commit();
        var blobClient = _aBSService.GetVideoBlob(video);
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
        var blobClient = _aBSService.GetVideoBlob(video);
        return null != blobClient
                   && await blobClient.ExistsAsync()
                   && blobClient.CanGenerateSasUri
               ? blobClient.GenerateSasUri(Azure.Storage.Sas.BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(12)).Query
               : null;
    }

    #region Dispose
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _publicUnitOfWork.Context.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
