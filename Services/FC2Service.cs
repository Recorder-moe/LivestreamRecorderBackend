using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using FC2MemberData = LivestreamRecorderBackend.Models.FC2MemberData._FC2MemberData;

namespace LivestreamRecorderBackend.Services;

public class Fc2Service(IHttpClientFactory httpClientFactory,
                        ILogger logger)
{
    private const string MemberApi = "https://live.fc2.com/api/memberApi.php";
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("client");

    public async Task<FC2MemberData?> GetFc2InfoDataAsync(string channelId, CancellationToken cancellation = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.PostAsync(
                requestUri: $@"{MemberApi}",
                content: new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        { "channel", "1" },
                        { "profile", "1" },
                        { "user", "0" },
                        { "streamid", channelId }
                    }),
                cancellationToken: cancellation);

            response.EnsureSuccessStatusCode();
            string jsonString = await response.Content.ReadAsStringAsync(cancellation);
            FC2MemberData? info = JsonSerializer.Deserialize<FC2MemberData>(jsonString);

            return info;
        }
        catch (Exception e)
        {
            logger.Error(e, "Get fc2 info failed. {channelId} Be careful if this happens repeatedly.", channelId);
            return null;
        }
    }
}
