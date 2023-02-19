using LivestreamRecorderBackend.DB.Core;
using LivestreamRecorderBackend.DB.Enum;
using LivestreamRecorderBackend.DB.Interfaces;
using Serilog;
using System;

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
