using LivestreamRecorderBackend.DB.Interfaces;
using LivestreamRecorderBackend.DB.Models;

namespace LivestreamRecorderBackend.DB.Core;

public class TransactionRepository : CosmosDbRepository<Transaction>, ITransactionRepository
{
    public TransactionRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public override string CollectionName { get; } = "Transactions";
}
