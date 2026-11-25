using InventoryAggregator.Core.Models;

namespace InventoryAggregator.Core.Interfaces
{
    public interface IInventoryClient
    {
        Task<IReadOnlyList<InventoryRecord>> GetAllRecordsAsync(CancellationToken cancellationToken = default);
    }
}
