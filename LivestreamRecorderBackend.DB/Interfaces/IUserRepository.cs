using LivestreamRecorderBackend.DB.Models;

namespace LivestreamRecorderBackend.DB.Interfaces;

public interface IUserRepository : ICosmosDbRepository<User>
{
}
