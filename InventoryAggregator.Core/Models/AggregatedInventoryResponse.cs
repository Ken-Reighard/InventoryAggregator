using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace InventoryAggregator.Core.Models
{
    public class AggregatedInventoryResponse
    {
        [JsonPropertyName("categories")] 
        public IReadOnlyList<CategorySummary> CategorySummaries { get; set; } = new List<CategorySummary>();
    }
}
