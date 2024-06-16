using System;

namespace LivestreamRecorderBackend.Helper;

public static class NameHelper
{
    /// <summary>
    ///     Change the id between platform type and database type. This is designed to prevent id conflict and invalid database
    ///     key.
    /// </summary>
    public static class ChangeId
    {
        public static class ChannelId
        {
            public static string PlatformType(string channelId, string platform)
            {
                return platform switch
                {
                    "Youtube" => channelId, // Youtube channelId already starts with "UC"
                    "Twitch" => channelId[2..],
                    "Twitcasting" => channelId[2..],
                    "FC2" => channelId[2..],
                    _ => throw new NotImplementedException()
                };
            }

            public static string DatabaseType(string channelId, string platform)
            {
                return platform switch
                {
                    "Youtube" => channelId, // Youtube channelId always starts with "UC"
                    "Twitch" => "TW" + channelId,
                    "Twitcasting" => "TC" + channelId,
                    "FC2" => "FC" + channelId,
                    _ => throw new NotImplementedException()
                };
            }
        }

        public static class VideoId
        {
            public static string PlatformType(string videoId, string platform)
            {
                return platform switch
                {
                    "Youtube" => videoId[1..],
                    "Twitch" => videoId[2..],
                    "Twitcasting" => videoId[2..],
                    "FC2" => videoId[2..],
                    _ => throw new NotImplementedException()
                };
            }

            public static string DatabaseType(string videoId, string platform)
            {
                return platform switch
                {
                    "Youtube" => "Y" + videoId,
                    "Twitch" => "TW" + videoId,
                    "Twitcasting" => "TC" + videoId,
                    "FC2" => "FC" + videoId,
                    _ => throw new NotImplementedException()
                };
            }
        }
    }
}
