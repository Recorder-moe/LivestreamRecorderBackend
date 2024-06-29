using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LivestreamRecorderBackend.Helper;
using LivestreamRecorderBackend.Interfaces;
using LivestreamRecorderBackend.Models;
using Serilog;
using SourceGenerationContext = LivestreamRecorderBackend.Json.SourceGenerationContext;

namespace LivestreamRecorderBackend.Services.PlatformService;

public class Fc2Service(IHttpClientFactory httpClientFactory,
                        ILogger logger) : IPlatformService
{
    private const string MemberApi = "https://live.fc2.com/api/memberApi.php";
    private static string PlatformName => "FC2";

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(SourceGenerationContext)} is set.")]
    public async Task<(string? avatarUrl, string? bannerUrl, string? channelName)> GetChannelData(
        string channelId,
        CancellationToken cancellation = default)
    {
        try
        {
            using HttpClient client = httpClientFactory.CreateClient("client");
            HttpResponseMessage response = await client.PostAsync(
                requestUri: $@"{MemberApi}",
                content: new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        { "channel", "1" },
                        { "profile", "1" },
                        { "user", "0" },
                        { "streamid", NameHelper.ChangeId.ChannelId.PlatformType(channelId, PlatformName) }
                    }),
                cancellationToken: cancellation);

            response.EnsureSuccessStatusCode();
            string jsonString = await response.Content.ReadAsStringAsync(cancellation);
            FC2MemberData? info = JsonSerializer.Deserialize<FC2MemberData>(
                jsonString,
                options: new JsonSerializerOptions
                {
                    TypeInfoResolver = SourceGenerationContext.Default
                });

            if (null == info)
            {
                logger.Warning("Failed to get channel info for {channelId}", channelId);
                return (null, null, null);
            }

            string? avatarUrl = info.Data.ProfileData.Image;
            string? bannerUrl = null;
            string? channelName = info.Data.ProfileData.Name;

            return (avatarUrl, bannerUrl, channelName);
        }
        catch (Exception)
        {
            logger.Error("Failed to get channel info for {channelId}", channelId);
            throw;
        }
    }
}
