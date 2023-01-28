using LivestreamRecorderBackend.DB.Interfaces;
using LivestreamRecorderBackend.DB.Models;

namespace LivestreamRecorderBackend.DB.Core;

public class UserRepository : CosmosDbRepository<User>, IUserRepository
{
    public UserRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public override string CollectionName { get; } = "Users";
}
