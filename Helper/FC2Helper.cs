using LivestreamRecorderService.Models;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Helper;

public static class FC2Helper
{
    private static ILogger Logger => Helper.Log.Logger;

    private const string _memberApi = "https://live.fc2.com/api/memberApi.php";

    public static async Task<FC2MemberData?> GetFC2InfoDataAsync(string channelId, CancellationToken cancellation = default)
    {
        try
        {
            using var client = new HttpClient();
            var response = await client.PostAsync(
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
            Logger.Error(e, "Get fc2 info failed. {channelId} Be careful if this happens repeatedly.", channelId);
            return null;
        }
    }
}
