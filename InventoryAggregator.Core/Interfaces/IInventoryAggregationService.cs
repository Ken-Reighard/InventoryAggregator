using InventoryAggregator.Core.Models;

namespace InventoryAggregator.Core.Interfaces
{
    public interface IInventoryAggregationService
    {
        Task<AggregatedInventoryResponse> GetAggregatedInventoryAsync(CancellationToken cancellationToken = default);
    }
}
