using Azure.Storage.Blobs.Models;
using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Helper;
using MimeMapping;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Services;

internal class ChannelService : IDisposable
{
    private static ILogger Logger => Helper.Log.Logger;
    private bool _disposedValue;
    private readonly IUnitOfWork _publicUnitOfWork;
    private readonly ChannelRepository _channelRepository;
    private readonly ABSservice _aBSService;

    public ChannelService()
    {
        (_, _publicUnitOfWork) = Helper.Database.MakeDBContext<PublicContext, UnitOfWork_Public>();
        _channelRepository = new ChannelRepository((UnitOfWork_Public)_publicUnitOfWork);

        _aBSService = new ABSservice();
    }

    internal Channel GetChannelById(string id) => _channelRepository.GetById(id);

    internal bool ChannelExists(string id) => _channelRepository.Exists(id);

    internal Channel AddChannel(string id, string source)
    {
        var channel = new Channel
        {
            id = id,
            Monitoring = true,
            Source = source,
        };
        _channelRepository.Add(channel);
        _publicUnitOfWork.Commit();
        return channel;
    }

    internal async Task UpdateChannelData(Channel channel, CancellationToken cancellation = default)
    {
        var avatarBlobUrl = channel.Avatar;
        var bannerBlobUrl = channel.Banner;
        var info = await Helper.YoutubeDL.GetInfoByYtdlpAsync(channel.id, cancellation);
        if (null == info)
        {
            Logger.Warning("Failed to get channel info for {channelId}", channel.id);
            return;
        }

        var thumbnails = info.Thumbnails.OrderByDescending(p => p.Preference).ToList();
        var avatarUrl = thumbnails.FirstOrDefault()?.Url;
        if (!string.IsNullOrEmpty(avatarUrl))
        {
            avatarBlobUrl = await DownloadImageAndUploadToBlobStorage(avatarUrl, $"avatar/{channel.id}", cancellation);
        }

        var bannerUrl = thumbnails.Skip(1).FirstOrDefault()?.Url;
        if (!string.IsNullOrEmpty(bannerUrl))
        {
            bannerBlobUrl = (await DownloadImageAndUploadToBlobStorage(bannerUrl, $"banner/{channel.id}", cancellation));
        }

        _publicUnitOfWork.Context.Entry(channel).Reload();
        channel = _channelRepository.LoadRelatedData(channel);
        channel.ChannelName = info.Uploader;
        channel.Avatar = avatarBlobUrl?.Replace("avatar/", "");
        channel.Banner = bannerBlobUrl?.Replace("banner/", "");
        _channelRepository.Update(channel);
        _publicUnitOfWork.Commit();
    }

    /// <summary>
    /// Download thumbnail.
    /// </summary>
    /// <param name="thumbnail">Url to download the thumbnail.</param>
    /// <param name="videoId"></param>
    /// <param name="cancellation"></param>
    /// <returns>Thumbnail file name with extension.</returns>
    protected async Task<string?> DownloadThumbnailAsync(string thumbnail, string videoId, CancellationToken cancellation = default)
        => string.IsNullOrEmpty(thumbnail)
            ? null
            : (await DownloadImageAndUploadToBlobStorage(thumbnail, $"thumbnails/{videoId}", cancellation))?.Replace("thumbnails/", "");

    /// <summary>
    /// Download image and upload it to Blob Storage
    /// </summary>
    /// <param name="url">Image source url to download.</param>
    /// <param name="path">Path in Blob storage (without extension)</param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    protected async Task<string?> DownloadImageAndUploadToBlobStorage(string url, string path, CancellationToken cancellation)
    {
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentNullException(nameof(url));
        }

        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        using var client = new HttpClient();
        var response = await client.GetAsync(url, cancellation);
        if (!response.IsSuccessStatusCode) return null;

        var contentType = response.Content.Headers.ContentType?.MediaType;
        var extension = MimeUtility.GetExtensions(contentType)?.FirstOrDefault();
        extension = extension == "jpeg" ? "jpg" : extension;
        string pathInStorage = $"{path}.{extension}";

        string tempPath = Path.GetTempFileName();
        tempPath = Path.ChangeExtension(tempPath, extension);
        using (var contentStream = await response.Content.ReadAsStreamAsync(cancellation))
        using (var fileStream = new FileStream(tempPath, FileMode.Create))
        {
            await contentStream.CopyToAsync(fileStream, cancellation);
        }

        List<Task> tasks = new();

        var blobClient = _aBSService.GetPublicBlob(pathInStorage)!;
        tasks.Add(blobClient.UploadAsync(
             path: tempPath,
             httpHeaders: new BlobHttpHeaders { ContentType = contentType },
             accessTier: AccessTier.Hot,
        cancellationToken: cancellation));

        var avifblobClient = _aBSService.GetPublicBlob($"{path}.avif")!;
        tasks.Add(avifblobClient.UploadAsync(
             path: await ImageHelper.ConvertToAvifAsync(tempPath),
             httpHeaders: new BlobHttpHeaders { ContentType = KnownMimeTypes.Avif },
             accessTier: AccessTier.Hot,
             cancellationToken: cancellation));

        await Task.WhenAll(tasks);

        File.Delete(tempPath);
        File.Delete(Path.ChangeExtension(tempPath, ".avif"));

        return pathInStorage;
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
