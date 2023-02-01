using LivestreamRecorderBackend.DB.Models;

namespace LivestreamRecorderBackend.DB.Interfaces;

public interface IChannelRepository : ICosmosDbRepository<Channel>
{
    IQueryable<Channel> GetMonitoringChannels();
}
