using InventoryAggregator.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventoryAggregator.Core.Interfaces
{
    public interface IInventoryClient
    {
        Task<IReadOnlyList<InventoryRecord>> GetAllRecordsAsync(CancellationToken cancellationToken = default);
    }
}
