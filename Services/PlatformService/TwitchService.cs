using System;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LivestreamRecorderBackend.Helper;
using LivestreamRecorderBackend.Interfaces;
using Serilog;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Api.Interfaces;

namespace LivestreamRecorderBackend.Services.PlatformService;

public class TwitchService(ILogger logger,
                           ITwitchAPI twitchApi) : IPlatformService
{
    public async Task<(string? avatarUrl, string? bannerUrl, string? channelName)> GetChannelData(string channelId, CancellationToken cancellation)
    {
        string login = NameHelper.ChangeId.ChannelId.PlatformType(channelId, "Twitch");
        GetUsersResponse? usersResponse = await GetUserAsync(login);

        if (null == usersResponse || usersResponse.Users.Length == 0)
        {
            logger.Error("Failed to get channel info for {channelId}", channelId);
            throw new HttpRequestException($"Failed to get channel info for {channelId}");
        }

        User user = usersResponse.Users.First();

        string avatarUrl = user.ProfileImageUrl.Replace("70x70", "300x300");
        string? bannerUrl = user.OfflineImageUrl;
        string? channelName = user.DisplayName;

        return (avatarUrl, bannerUrl, channelName);
    }

    public virtual async Task<GetUsersResponse?> GetUserAsync(string login)
    {
        EnsureTwitchSetup();
        GetUsersResponse? usersResponse = await twitchApi.Helix.Users.GetUsersAsync(logins: [login]);
        return usersResponse;
    }

    public void EnsureTwitchSetup()
    {
        string? twitchClientId = Environment.GetEnvironmentVariable("Twitch_ClientId");
        string? twitchSecret = Environment.GetEnvironmentVariable("Twitch_ClientSecret");
        if (!string.IsNullOrEmpty(twitchClientId) && !string.IsNullOrEmpty(twitchSecret)) return;
        const string message = "Please setup 'Twitch_ClientId' and 'Twitch_ClientSecret' before creating Twitch channels.";
        var e = new ConfigurationErrorsException(message);
        logger.Error(e, message);
        throw e;
    }
}
