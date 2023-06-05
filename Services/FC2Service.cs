using LivestreamRecorderService.Models;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Services;

public class FC2Service
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private const string _memberApi = "https://live.fc2.com/api/memberApi.php";


    public FC2Service(
        IHttpClientFactory httpClientFactory,
        ILogger logger)
    {
        _httpClient = httpClientFactory.CreateClient("client");
        _logger = logger;
    }

    public async Task<FC2MemberData?> GetFC2InfoDataAsync(string channelId, CancellationToken cancellation = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                requestUri: $@"{_memberApi}",
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
            string jsonString = await response.Content.ReadAsStringAsync(cancellation);
            FC2MemberData? info = JsonConvert.DeserializeObject<FC2MemberData>(jsonString);

            return info;
        }
        catch (Exception e)
        {
            _logger.Error(e, "Get fc2 info failed. {channelId} Be careful if this happens repeatedly.", channelId);
            return null;
        }
    }
}
