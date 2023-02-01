using LivestreamRecorderBackend.DB.Models;

namespace LivestreamRecorderBackend.DB.Interfaces;

public interface IVideoRepository : ICosmosDbRepository<Video> { }
