using System;
using System.Collections.Generic;
using System.Text;

namespace InventoryAggregator.Core.Models
{
    public class MostRecentlyUpdatedItem
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public DateTimeOffset LastUpdated { get; set; }
    }
}
