#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LivestreamRecorder.DB.Interfaces;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderBackend.Helper;
using LivestreamRecorderBackend.Interfaces;
using LivestreamRecorderBackend.Models;
using MimeMapping;
using Serilog;

namespace LivestreamRecorderBackend.Services;

public class ChannelService(ILogger logger,
                            IStorageService storageService,
                            IChannelRepository channelRepository,
                            UnitOfWork_Public unitOfWorkPublic,
                            Fc2Service fC2Service,
                            IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("client");
#pragma warning disable CA1859
    private readonly IUnitOfWork _unitOfWorkPublic = unitOfWorkPublic;
#pragma warning restore CA1859

    public Task<Channel?> GetByChannelIdAndSourceAsync(string channelId, string source)
    {
        return channelRepository.GetChannelByIdAndSourceAsync(channelId, source);
    }

    internal async Task<Channel> AddChannelAsync(string id, string source, string channelName)
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
        if (source == "FC2") channel.Hide = true;

        await channelRepository.AddOrUpdateAsync(channel);
        _unitOfWorkPublic.Commit();
        return channel;
    }

    internal async Task UpdateChannelDataAsync(Channel channel,
                                               bool autoUpdateInfo,
                                               string? name = null,
                                               string? avatarUrl = null,
                                               string? bannerUrl = null,
                                               CancellationToken cancellation = default)
    {
        string channelName = channel.ChannelName;
        string? avatarBlobUri = channel.Avatar;
        string? bannerBlobUri = channel.Banner;

        if (autoUpdateInfo)
            switch (channel.Source)
            {
                case "Youtube":
                {
                    YtdlpVideoData._YtdlpVideoData? info =
                        await YoutubeDL.GetInfoByYtdlpAsync($"https://www.youtube.com/channel/{channel.id}", cancellation);

                    if (null == info)
                    {
                        logger.Warning("Failed to get channel info for {channelId}", channel.id);
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
                    FC2MemberData._FC2MemberData? info = await fC2Service.GetFc2InfoDataAsync(channel.id, cancellation);
                    if (null == info)
                    {
                        logger.Warning("Failed to get channel info for {channelId}", channel.id);
                        return;
                    }

                    name = info.Data.ProfileData.Name;
                    avatarUrl = info.Data.ProfileData.Image;
                }

                    break;
                // ReSharper disable once RedundantEmptySwitchSection
                default:
                    // Only download images for Youtube and FC2
                    break;
            }

        if (!string.IsNullOrEmpty(name)) channelName = name;

        if (!string.IsNullOrEmpty(avatarUrl) && avatarUrl.StartsWith("http"))
            avatarBlobUri = await DownloadImageAndUploadToBlobStorageAsync(avatarUrl, $"avatar/{channel.id}", cancellation);

        if (!string.IsNullOrEmpty(bannerUrl) && bannerUrl.StartsWith("http"))
            bannerBlobUri = await DownloadImageAndUploadToBlobStorageAsync(bannerUrl, $"banner/{channel.id}", cancellation);

        await channelRepository.ReloadEntityFromDBAsync(channel);
        channel.ChannelName = channelName;
        channel.Avatar = avatarBlobUri?.Replace("avatar/", "");
        channel.Banner = bannerBlobUri?.Replace("banner/", "");
        await channelRepository.AddOrUpdateAsync(channel);
        _unitOfWorkPublic.Commit();
    }

    /// <summary>
    ///     Download thumbnail.
    /// </summary>
    /// <param name="thumbnail">Url to download the thumbnail.</param>
    /// <param name="videoId"></param>
    /// <param name="cancellation"></param>
    /// <returns>Thumbnail file name with extension.</returns>
    protected async Task<string?> DownloadThumbnailAsync(string thumbnail, string videoId, CancellationToken cancellation = default)
    {
        return string.IsNullOrEmpty(thumbnail)
            ? null
            : (await DownloadImageAndUploadToBlobStorageAsync(thumbnail, $"thumbnails/{videoId}", cancellation))?.Replace("thumbnails/", "");
    }

    /// <summary>
    ///     Download image and upload it to Blob Storage
    /// </summary>
    /// <param name="url">Image source url to download.</param>
    /// <param name="path">Path in Blob storage (without extension)</param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    private async Task<string?> DownloadImageAndUploadToBlobStorageAsync(string url, string path, CancellationToken cancellation)
    {
        if (string.IsNullOrEmpty(url)) throw new ArgumentNullException(nameof(url));

        if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

        HttpResponseMessage response = await _httpClient.GetAsync(url, cancellation);
        if (!response.IsSuccessStatusCode) return null;

        string? contentType = response.Content.Headers.ContentType?.MediaType;
        string? extension = MimeUtility.GetExtensions(contentType)?.FirstOrDefault();
        extension = extension == "jpeg" ? "jpg" : extension;
        var pathInStorage = $"{path}.{extension}";

        string tempPath = Path.GetTempFileName();
        tempPath = Path.ChangeExtension(tempPath, extension);
        await using (Stream contentStream = await response.Content.ReadAsStreamAsync(cancellation))
        await using (var fileStream = new FileStream(tempPath, FileMode.Create))
        {
            await contentStream.CopyToAsync(fileStream, cancellation);
        }

        List<Task> tasks =
        [
            storageService.UploadPublicFileAsync(contentType: contentType,
                                                 pathInStorage: pathInStorage,
                                                 filePathToUpload: tempPath,
                                                 cancellation: cancellation),

            storageService.UploadPublicFileAsync(contentType: KnownMimeTypes.Avif,
                                                 pathInStorage: $"{path}.avif",
                                                 filePathToUpload: await ImageHelper.ConvertToAvifAsync(tempPath),
                                                 cancellation: cancellation)
        ];

        await Task.WhenAll(tasks);

        File.Delete(tempPath);
        File.Delete(Path.ChangeExtension(tempPath, ".avif"));

        return pathInStorage;
    }

    public async Task EditMonitoringAsync(string channelId, string source, bool enable)
    {
        Channel? channel = await channelRepository.GetChannelByIdAndSourceAsync(channelId, source);
        if (null == channel) throw new EntryPointNotFoundException($"Channel {channelId} not found.");

        channel.Monitoring = enable;
        await channelRepository.AddOrUpdateAsync(channel);
        _unitOfWorkPublic.Commit();
    }

    public async Task EditHidingAsync(string channelId, string source, bool hide)
    {
        Channel? channel = await channelRepository.GetChannelByIdAndSourceAsync(channelId, source);
        if (null == channel) throw new EntryPointNotFoundException($"Channel {channelId} not found.");

        channel.Hide = hide;
        await channelRepository.AddOrUpdateAsync(channel);
        _unitOfWorkPublic.Commit();
    }
}
