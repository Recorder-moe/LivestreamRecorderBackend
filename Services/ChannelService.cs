using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.Helper;
using LivestreamRecorderBackend.Interfaces;
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

public class ChannelService
{
    private readonly IUnitOfWork _unitOfWork_Public;
    private readonly FC2Service _fC2Service;
    private readonly HttpClient _httpClient;
    private readonly IChannelRepository _channelRepository;
    private readonly ILogger _logger;
    private readonly IStorageService _storageService;

    public ChannelService(
        ILogger logger,
        IStorageService storageService,
        IChannelRepository channelRepository,
        UnitOfWork_Public unitOfWork_Public,
        FC2Service fC2Service,
        IHttpClientFactory httpClientFactory)
    {
        _channelRepository = channelRepository;
        _unitOfWork_Public = unitOfWork_Public;
        _fC2Service = fC2Service;
        _httpClient = httpClientFactory.CreateClient("client");
        _logger = logger;
        _storageService = storageService;
    }

    internal Channel GetChannelById(string id) => _channelRepository.GetById(id);

    internal bool ChannelExists(string id) => _channelRepository.Exists(id);

    internal Channel AddChannel(string id, string source, string channelName)
    {
        var channel = new Channel
        {
            id = id,
            ChannelName = channelName,
            Monitoring = false,
            Source = source,
            Hide = false
        };

        // Hide FC2 channels by default
        if (source == "FC2")
        {
            channel.Hide = true;
        }

        _channelRepository.Add(channel);
        _unitOfWork_Public.Commit();
        return channel;
    }

    internal async Task UpdateChannelData(Channel channel, bool autoUpdateInfo, string? name = null, string? avatarUrl = null, string? bannerUrl = null, CancellationToken cancellation = default)
    {
        var channelName = channel.ChannelName;
        var avatarBlobUri = channel.Avatar;
        var bannerBlobUri = channel.Banner;

        if (autoUpdateInfo)
        {
            switch (channel.Source)
            {
                case "Youtube":
                    {
                        var info = await Helper.YoutubeDL.GetInfoByYtdlpAsync($"https://www.youtube.com/channel/{channel.id}", cancellation);
                        if (null == info)
                        {
                            _logger.Warning("Failed to get channel info for {channelId}", channel.id);
                            return;
                        }

                        name = info.Uploader;

                        var thumbnails = info.Thumbnails.OrderByDescending(p => p.Preference).ToList();
                        avatarUrl = thumbnails.FirstOrDefault()?.Url;
                        bannerUrl = thumbnails.Skip(1).FirstOrDefault()?.Url;
                    }
                    break;
                case "FC2":
                    {
                        var info = await _fC2Service.GetFC2InfoDataAsync(channel.id, cancellation);
                        if (null == info)
                        {
                            _logger.Warning("Failed to get channel info for {channelId}", channel.id);
                            return;
                        }

                        name = info.Data.ProfileData.Name;
                        avatarUrl = info.Data.ProfileData.Image;
                    }
                    break;
                default:
                    break;
            }
        }

        if (!string.IsNullOrEmpty(name))
        {
            channelName = name;
        }

        if (!string.IsNullOrEmpty(avatarUrl) && avatarUrl.StartsWith("http"))
        {
            avatarBlobUri = await DownloadImageAndUploadToBlobStorage(avatarUrl, $"avatar/{channel.id}", cancellation);
        }

        if (!string.IsNullOrEmpty(bannerUrl) && bannerUrl.StartsWith("http"))
        {
            bannerBlobUri = await DownloadImageAndUploadToBlobStorage(bannerUrl, $"banner/{channel.id}", cancellation);
        }

        _unitOfWork_Public.Context.Entry(channel).Reload();
        channel = _channelRepository.LoadRelatedData(channel);
        channel.ChannelName = channelName;
        channel.Avatar = avatarBlobUri?.Replace("avatar/", "");
        channel.Banner = bannerBlobUri?.Replace("banner/", "");
        _channelRepository.Update(channel);
        _unitOfWork_Public.Commit();
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

        var response = await _httpClient.GetAsync(url, cancellation);
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

        List<Task> tasks = new()
        {
            _storageService.UploadPublicFile(contentType: contentType,
                                             pathInStorage: pathInStorage,
                                             filePathToUpload: tempPath,
                                             cancellation: cancellation),
            _storageService.UploadPublicFile(contentType: KnownMimeTypes.Avif,
                                             pathInStorage: $"{path}.avif",
                                             filePathToUpload: await ImageHelper.ConvertToAvifAsync(tempPath),
                                             cancellation: cancellation)
        };

        await Task.WhenAll(tasks);

        File.Delete(tempPath);
        File.Delete(Path.ChangeExtension(tempPath, ".avif"));

        return pathInStorage;
    }

    public void EnableMonitoring(string channelId)
    {
        var channel = _channelRepository.GetById(channelId);
        channel.Monitoring = true;
        _channelRepository.Update(channel);
        _unitOfWork_Public.Commit();
    }

    public void DisableMonitoring(string channelId)
    {
        var channel = _channelRepository.GetById(channelId);
        channel.Monitoring = false;
        _channelRepository.Update(channel);
        _unitOfWork_Public.Commit();
    }
}
