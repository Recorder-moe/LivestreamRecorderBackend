using System.Linq;
using System.Threading.Tasks;
using LivestreamRecorderBackend.Helper;
using Serilog;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Api.Interfaces;

namespace LivestreamRecorderBackend.Services.PlatformService;

public class TwitchService(ILogger logger,
                           ITwitchAPI twitchApi)
{
    private static string PlatformName => "Twitch";

    public async Task<(string? avatarUrl, string? bannerUrl, string? channelName)> GetChannelData(string channelId)
    {
        GetUsersResponse? usersResponse =
            await twitchApi.Helix.Users.GetUsersAsync(logins: [NameHelper.ChangeId.ChannelId.PlatformType(channelId, PlatformName)]);

        if (null == usersResponse || usersResponse.Users.Length == 0)
        {
            logger.Warning("Failed to get channel info for {channelId}", channelId);
            return (null, null, null);
        }

        User user = usersResponse.Users.First();

        string avatarUrl = user.ProfileImageUrl.Replace("70x70", "300x300");
        string? bannerUrl = user.OfflineImageUrl;
        string? channelName = user.DisplayName;

        return (avatarUrl, bannerUrl, channelName);
    }
}
