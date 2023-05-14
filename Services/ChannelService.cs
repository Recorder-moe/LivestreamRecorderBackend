using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using System;

namespace LivestreamRecorderBackend.Services;

internal class ChannelService : IDisposable
{
    private bool _disposedValue;
    private readonly IUnitOfWork _publicUnitOfWork;
    private readonly ChannelRepository _channelRepository;


    public ChannelService()
    {
        (_, _publicUnitOfWork) = Helper.Database.MakeDBContext<PublicContext, UnitOfWork_Public>();
        _channelRepository = new ChannelRepository((UnitOfWork_Public)_publicUnitOfWork);
    }

    internal Channel GetChannelById(string id) => _channelRepository.GetById(id);

    internal bool ChannelExists(string id) => _channelRepository.Exists(id);

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
