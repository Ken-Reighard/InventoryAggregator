using System;
using System.Collections.Generic;
using System.Text;

namespace InventoryAggregator.Core.Models
{
    public class AggregatedInventoryResponse
    {
        public IReadOnlyList<CategorySummary> CategorySummaries { get; set; } = new List<CategorySummary>();
    }
}
