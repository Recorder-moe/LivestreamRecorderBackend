using LivestreamRecorderBackend.DB.Models;

namespace LivestreamRecorderBackend.DB.Interfaces;

public interface ITransactionRepository : ICosmosDbRepository<Transaction>
{
}
