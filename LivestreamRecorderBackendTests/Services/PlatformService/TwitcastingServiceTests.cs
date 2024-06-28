using NUnit.Framework;
using Moq;
using Serilog;
using LivestreamRecorderBackend.Services.PlatformService;

namespace LivestreamRecorderBackendTests.Services.PlatformService;

[TestFixture]
public class TwitcastingServiceTests
{
    private Mock<ILogger> _mockLogger;
    private TwitcastingService _twitcastingService;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
        _twitcastingService = new TwitcastingService(_mockLogger.Object);
    }

    [Test]
    public async Task GetChannelData_ReturnsCorrectData_WhenPageIsLoaded()
    {
        // Arrange
        const string channelId = "TCt3c_o0o";
        const string expectedAvatarUrl = "https://imagegw02.twitcasting.tv/image3s/pbs.twimg.com/profile_images/1653470085691084800/ysy4H2s7.jpg";
        const string expectedBannerUrl = "https://image.twitcasting.tv/image3/headers/12/aa18bbafa16a7d.jpg";
        const string expectedChannelName = "炭酸ちゃん";

        // Act
        (string? avatarUrl, string? bannerUrl, string? channelName) = await _twitcastingService.GetChannelData(channelId, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(avatarUrl, Is.EqualTo(expectedAvatarUrl));
            Assert.That(bannerUrl, Is.EqualTo(expectedBannerUrl));
            Assert.That(channelName, Is.EqualTo(expectedChannelName));
        });
    }
}
