using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Enum;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
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

    public VideoService()
    {
        (_, _publicUnitOfWork) = Helper.Database.MakeDBContext<PublicContext>();
        _videoRepository = new VideoRepository(_publicUnitOfWork);
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

    /// <summary>
    /// Get SAS token for video.
    /// </summary>
    /// <param name="videoId"></param>
    /// <param name="blobContainerClient"></param>
    /// <returns>SAS uri</returns>
    internal async Task<string?> GetSASTokenAsync(string videoId, Azure.Storage.Blobs.BlobContainerClient blobContainerClient)
    {
        var video = GetVideoById(videoId);
        var blobClient = blobContainerClient.GetBlobClient($@"/videos/{video.Filename}");
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
