using System;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using LivestreamRecorderBackend.Helper;
using LivestreamRecorderBackend.Interfaces;
using Serilog;

namespace LivestreamRecorderBackend.Services.PlatformService;

public class TwitcastingService(ILogger logger) : IPlatformService
{
    private static string PlatformName => "Twitcasting";

    public async Task<(string? avatarUrl, string? bannerUrl, string? channelName)> GetChannelData(string channelId, CancellationToken cancellation)
    {
        HtmlDocument htmlDoc = await new HtmlWeb().LoadFromWebAsync(
            $"https://twitcasting.tv/{NameHelper.ChangeId.ChannelId.PlatformType(channelId, PlatformName)}",
            cancellation);

        if (null == htmlDoc)
        {
            logger.Warning("Failed to get channel page for {channelId}", channelId);
            return (null, null, null);
        }

        string? avatarUrl = GetAvatarBlobUrl(htmlDoc);
        string? bannerUrl = GetBannerBlobUrl(htmlDoc);
        string? channelName = GetChannelName(htmlDoc);

        return (avatarUrl, bannerUrl, channelName);
    }

    private static string? GetChannelName(HtmlDocument htmlDoc)
    {
        HtmlNode? nameNode1 = htmlDoc.DocumentNode.SelectSingleNode("//span[@class='tw-user-nav-name']");
        HtmlNode? nameNode2 = htmlDoc.DocumentNode.SelectSingleNode("//span[@class='tw-user-nav2-name']");
        return nameNode1?.InnerText ?? nameNode2?.InnerText;
    }

    private static string? ExtractBackgroundImageUrl(string style)
    {
        if (string.IsNullOrEmpty(style)) return null;

        const string searchString = "background-image: url(";
        int startIndex = style.IndexOf(searchString, StringComparison.Ordinal);
        if (startIndex == -1) return null;

        startIndex += searchString.Length;
        int endIndex = style.IndexOf(')', startIndex);
        if (endIndex == -1) return null;

        string url = style[startIndex..endIndex].Trim('\'', '\"');
        if (url.StartsWith("//")) url = "https:" + url;

        return url;
    }

    private static string? GetBannerBlobUrl(HtmlDocument htmlDoc)
    {
        HtmlNode? bannerNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='tw-user-banner-image']");
        string? bannerUrl = ExtractBackgroundImageUrl(bannerNode?.GetAttributeValue("style", "") ?? "");

        return bannerUrl;
    }

    private static string? GetAvatarBlobUrl(HtmlDocument htmlDoc)
    {
        HtmlNode? avatarImgNode = htmlDoc.DocumentNode.SelectSingleNode("//a[@class='tw-user-nav-icon']/img");
        HtmlNode? avatarImgNode2 = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='tw-user-nav2-icon']/img");
        string avatarUrl = (avatarImgNode?.Attributes["src"]?.Value
                            ?? avatarImgNode2?.Attributes["src"]?.Value
                            ?? "")
            .Replace("_bigger", "");

        if (avatarUrl.StartsWith("//")) avatarUrl = "https:" + avatarUrl;

        return avatarUrl;
    }
}
