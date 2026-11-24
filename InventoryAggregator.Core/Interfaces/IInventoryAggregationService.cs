using InventoryAggregator.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventoryAggregator.Core.Interfaces
{
    public interface IInventoryAggregationService
    {
        Task<AggregatedInventoryResponse> GetAggregatedInventoryAsync(CancellationToken cancellationToken = default);
    }
}
