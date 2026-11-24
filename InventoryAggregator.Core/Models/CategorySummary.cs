using InventoryAggregator.Core.DTO;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventoryAggregator.Core.Models
{
    public class CategorySummary
    {
        public string Category { get; set; } = string.Empty;

        public decimal TotalInventoryValue { get; set; }

        public int AvailableRecordCount { get; set; }

        public MostRecentlyUpdatedItem MostRecentlyUpdated { get; set; } = new MostRecentlyUpdatedItem();

        public IReadOnlyList<CategoryItem> Items { get; set; } = Array.Empty<CategoryItem>();
    }
}

