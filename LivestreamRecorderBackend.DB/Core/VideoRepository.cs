using LivestreamRecorderBackend.DB.Interfaces;
using LivestreamRecorderBackend.DB.Models;

namespace LivestreamRecorderBackend.DB.Core;

public class VideoRepository : CosmosDbRepository<Video>, IVideoRepository
{
    public VideoRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public override string CollectionName { get; } = "Videos";
}
