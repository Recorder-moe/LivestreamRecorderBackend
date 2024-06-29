using System.Threading;
using System.Threading.Tasks;

namespace LivestreamRecorderBackend.Interfaces;
public interface IPlatformService
{
    Task<(string? avatarUrl, string? bannerUrl, string? channelName)> GetChannelData(string channelId, CancellationToken cancellation = default);
}