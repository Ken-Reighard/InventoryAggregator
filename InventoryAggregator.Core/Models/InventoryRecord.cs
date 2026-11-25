namespace InventoryAggregator.Core.Models
{
    public class InventoryRecord
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public int Quantity { get; set; }

        public DateTimeOffset LastUpdated { get; set; }
    }
}
