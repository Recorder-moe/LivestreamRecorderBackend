using System;

namespace LivestreamRecorderBackend.Helper;

public static class NameHelper
{
    /// <summary>
    /// Change the id between platform type and database type. This is designed to prevent id conflict and invalid database key.
    /// </summary>
    public static class ChangeId
    {
        public static class ChannelId
        {
            public static string PlatformType(string channelId, string Platform)
                => Platform switch
                {
                    "Youtube" => channelId, // Youtube channelId already starts with "UC"
                    "Twitch" => channelId.StartsWith("TW") ? channelId[2..] : channelId,
                    "Twitcasting" => channelId.StartsWith("TC") ? channelId[2..] : channelId,
                    "FC2" => channelId.StartsWith("FC") ? channelId[2..] : channelId,
                    _ => throw new NotImplementedException(),
                };

            public static string DatabaseType(string channelId, string Platform)
                => Platform switch
                {
                    "Youtube" => channelId, // Youtube channelId always starts with "UC"
                    "Twitch" => channelId.StartsWith("TW") ? channelId : "TW" + channelId,
                    "Twitcasting" => channelId.StartsWith("TC") ? channelId : "TC" + channelId,
                    "FC2" => channelId.StartsWith("FC") ? channelId : "FC" + channelId,
                    _ => throw new NotImplementedException(),
                };
        }

        public static class VideoId
        {
            public static string PlatformType(string videoId, string Platform)
                => Platform switch
                {
                    "Youtube" => videoId.StartsWith('Y') ? videoId[1..] : videoId,
                    "Twitch" => videoId.StartsWith("TW") ? videoId[2..] : videoId,
                    "Twitcasting" => videoId.StartsWith("TC") ? videoId[2..] : videoId,
                    "FC2" => videoId.StartsWith("FC") ? videoId[2..] : videoId,
                    _ => throw new NotImplementedException(),
                };

            public static string DatabaseType(string videoId, string Platform)
                => Platform switch
                {
                    "Youtube" => videoId.StartsWith('Y') ? videoId : "Y" + videoId,
                    "Twitch" => videoId.StartsWith("TW") ? videoId : "TW" + videoId,
                    "Twitcasting" => videoId.StartsWith("TC") ? videoId : "TC" + videoId,
                    "FC2" => videoId.StartsWith("FC") ? videoId : "FC" + videoId,
                    _ => throw new NotImplementedException(),
                };
        }
    }
}
