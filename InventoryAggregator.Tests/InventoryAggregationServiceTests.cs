using InventoryAggregator.Core.Models;
using InventoryAggregator.Core.Services;

namespace InventoryAggregator.Tests;

public class InventoryAggregationServiceTests
{
    [Fact]
    public async Task GetAggregatedInventoryAsync_NormalizesCategories_ExcludesZeroQuantity_AndComputesTotals()
    {
        // Arrange
        var t1 = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 9, 5, 15, 30, 0, TimeSpan.Zero);

        var records = new List<InventoryRecord>
        {
            new() { Id = 1, Name = "Laptop", Category = "  electronics ", Price = 1000m, Quantity = 2, LastUpdated = t1 },
            new() { Id = 2, Name = "Mouse", Category = "ELECTRONICS", Price = 25m, Quantity = 4, LastUpdated = t2 },
            new() { Id = 3, Name = "Out of Stock Monitor", Category = "Electronics", Price = 300m, Quantity = 0, LastUpdated = t2 }
        };

        var service = new InventoryAggregationService(new FakeInventoryClient(records));

        // Act
        var result = await service.GetAggregatedInventoryAsync();

        // Assert
        var electronics = Assert.Single(result.CategorySummaries);
        Assert.Equal("Electronics", electronics.Category);
        Assert.Equal(2100m, electronics.TotalInventoryValue); // (1000 * 2) + (25 * 4)
        Assert.Equal(2, electronics.AvailableRecordCount);
        Assert.Equal(2, electronics.MostRecentlyUpdated.Id);
        Assert.Equal(2, electronics.Items.Count);
    }

    [Fact]
    public async Task GetAggregatedInventoryAsync_NormalizesCategories_ExcludesZeroQuantity_AndComputesTotals_MultiCategory()
    {
        // Arrange
        var t1 = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 9, 5, 15, 30, 0, TimeSpan.Zero);
        var t3= new DateTimeOffset(2026, 9, 7, 15, 30, 0, TimeSpan.Zero);

        var records = new List<InventoryRecord>
        {
            new() { Id = 1, Name = "Laptop", Category = "  electronics ", Price = 1000m, Quantity = 2, LastUpdated = t1 },
            new() { Id = 2, Name = "Mouse", Category = "ELECTRONICS", Price = 25m, Quantity = 4, LastUpdated = t2 },
            new() { Id = 3, Name = "Out of Stock Monitor", Category = "Electronics", Price = 300m, Quantity = 0, LastUpdated = t2 },
            new() { Id = 4, Name = "Train", Category = "  toys ", Price = 100m, Quantity = 7, LastUpdated = t1 },
            new() { Id = 5, Name = "Ball", Category = "TOYS", Price = 5m, Quantity = 200, LastUpdated = t2 },
            new() { Id = 6, Name = "Glove", Category = "Toys", Price = 40m, Quantity = 5, LastUpdated = t3 }
        };

        var service = new InventoryAggregationService(new FakeInventoryClient(records));

        // Act
        var result = await service.GetAggregatedInventoryAsync();

        // Assert
        Assert.Equal(2, result.CategorySummaries.Count());
        var electronics = Assert.Single(result.CategorySummaries, e => e.Category == "Electronics");
        Assert.Equal("Electronics", electronics.Category);
        Assert.Equal(2100m, electronics.TotalInventoryValue); // (1000 * 2) + (25 * 4)
        Assert.Equal(2, electronics.AvailableRecordCount);
        Assert.Equal(2, electronics.MostRecentlyUpdated.Id);
        Assert.Equal(2, electronics.Items.Count);
        var toys = Assert.Single(result.CategorySummaries, e => e.Category == "Toys");
        Assert.Equal("Toys", toys.Category);
        Assert.Equal(1900m, toys.TotalInventoryValue); // (100 * 7) + (200 * 5) + (40 * 5)
        Assert.Equal(6, toys.MostRecentlyUpdated.Id);
        Assert.Equal(3, toys.Items.Count);
    }
}
