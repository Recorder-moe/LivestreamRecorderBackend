using LivestreamRecorderBackend.DB.Interfaces;
using LivestreamRecorderBackend.DB.Models;

namespace LivestreamRecorderBackend.DB.Core;

public class ChannelRepository : CosmosDbRepository<Channel>, IChannelRepository
{
    public ChannelRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public IQueryable<Channel> GetMonitoringChannels() => Where(p => p.Monitoring);

    public override string CollectionName { get; } = "Channels";
}
