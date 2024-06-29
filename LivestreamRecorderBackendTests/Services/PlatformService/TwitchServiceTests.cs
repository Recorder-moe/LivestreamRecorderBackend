using LivestreamRecorderBackend.Services.PlatformService;
using Moq;
using NUnit.Framework;
using Serilog;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Api.Interfaces;

namespace LivestreamRecorderBackendTests.Services.PlatformService;

[TestFixture]
public class TwitchServiceTests
{
    private class TestableUser : User
    {
        public new string ProfileImageUrl
        {
            set => base.ProfileImageUrl = value;
        }

        public new string OfflineImageUrl
        {
            set => base.OfflineImageUrl = value;
        }

        public new string DisplayName
        {
            set => base.DisplayName = value;
        }
    }

    private class TestableGetUsersResponse : GetUsersResponse
    {
        public new User[] Users
        {
            set => base.Users = value;
        }
    }

    [Test]
    public async Task GetChannelData_ReturnsCorrectData_WhenUserExists()
    {
        // Arrange
        const string channelId = "TWsudayoruka";
        const string expectedAvatarUrl =
            "https://static-cdn.jtvnw.net/jtv_user_pictures/fc2cf7d8-953a-43f8-bb68-4bc774b6df7d-profile_image-300x300.png";

        const string expectedBannerUrl = "https://static-cdn.jtvnw.net/jtv_user_pictures/69ffad3c-7ef8-494e-9ca5-903229a422df-profile_banner-480.png";
        const string expectedChannelName = "須多夜花";

        var testableGetUsersResponse = new TestableGetUsersResponse
        {
            Users =
            [
                new TestableUser
                {
                    ProfileImageUrl =
                        "https://static-cdn.jtvnw.net/jtv_user_pictures/fc2cf7d8-953a-43f8-bb68-4bc774b6df7d-profile_image-70x70.png",
                    OfflineImageUrl =
                        "https://static-cdn.jtvnw.net/jtv_user_pictures/69ffad3c-7ef8-494e-9ca5-903229a422df-profile_banner-480.png",
                    DisplayName = "須多夜花"
                }
            ]
        };

        var mockTwitchService = new Mock<TwitchService>(new Mock<ILogger>().Object, new Mock<ITwitchAPI>().Object);
        mockTwitchService.Setup(p => p.GetUserAsync("sudayoruka"))
                         .ReturnsAsync(testableGetUsersResponse)
                         .Verifiable(Times.Once);

        // Act
        (string? avatarUrl, string? bannerUrl, string? channelName) =
            await mockTwitchService.Object.GetChannelData(channelId, CancellationToken.None);

        // Assert
        Mock.VerifyAll();
        Assert.Multiple(() =>
        {
            Assert.That(avatarUrl, Is.EqualTo(expectedAvatarUrl));
            Assert.That(bannerUrl, Is.EqualTo(expectedBannerUrl));
            Assert.That(channelName, Is.EqualTo(expectedChannelName));
        });
    }
}
