using Serilog;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FC2MemberData = LivestreamRecorderBackend.Models.FC2MemberData._FC2MemberData;

namespace LivestreamRecorderBackend.Services;

public class Fc2Service
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private const string MemberApi = "https://live.fc2.com/api/memberApi.php";


    public Fc2Service(
        IHttpClientFactory httpClientFactory,
        ILogger logger)
    {
        _httpClient = httpClientFactory.CreateClient("client");
        _logger = logger;
    }

    public async Task<FC2MemberData?> GetFc2InfoDataAsync(string channelId, CancellationToken cancellation = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                requestUri: $@"{MemberApi}",
                content: new FormUrlEncodedContent(
                    new Dictionary<string, string>()
                    {
                        { "channel", "1" },
                        { "profile", "1" },
                        { "user", "0" },
                        { "streamid", channelId }
                    }),
                cancellationToken: cancellation);

            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync(cancellation);
            var info = JsonSerializer.Deserialize<FC2MemberData>(jsonString);

            return info;
        }
        catch (Exception e)
        {
            _logger.Error(e, "Get fc2 info failed. {channelId} Be careful if this happens repeatedly.", channelId);
            return null;
        }
    }
}
