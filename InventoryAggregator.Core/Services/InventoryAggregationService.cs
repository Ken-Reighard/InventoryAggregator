using InventoryAggregator.Core.Interfaces;
using InventoryAggregator.Core.Models;
using System.Globalization;

namespace InventoryAggregator.Core.Services
{
    public class InventoryAggregationService : IInventoryAggregationService
    {
        private readonly IInventoryClient _inventoryClient;

        public InventoryAggregationService(IInventoryClient inventoryClient)
        {
            _inventoryClient = inventoryClient ?? throw new ArgumentNullException(nameof(inventoryClient)); ;
        }

        public async Task<AggregatedInventoryResponse> GetAggregatedInventoryAsync(CancellationToken cancellationToken = default)
        {
            var records = await _inventoryClient.GetAllRecordsAsync(cancellationToken);

            var categories = records.
                Where(r => r.Quantity > 0).
                GroupBy(r => NormalizeCategory(r.Category)).
                OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase).
                Select(g =>
                {
                    var mostRecent = g.
                    OrderByDescending(r => r.LastUpdated).
                    ThenByDescending(r => r.Id).
                    First();

                    return new CategorySummary
                    {
                        Category = g.Key,
                        TotalInventoryValue = g.Sum(r => r.Quantity * r.Price),
                        AvailableRecordCount = g.Count(),
                        MostRecentlyUpdated = new MostRecentlyUpdatedItem
                        {
                            Id = mostRecent.Id,
                            Name = mostRecent.Name,
                            LastUpdated = mostRecent.LastUpdated
                        },
                        Items = g
                            .OrderBy(r => r.Id)
                            .Select(r => new CategoryItem
                            {
                                Id = r.Id,
                                Name = r.Name,
                                Price = r.Price,
                                Quantity = r.Quantity,
                                LastUpdated = r.LastUpdated
                            })
                            .ToList()
                    };
                })
                .ToList();

            return new AggregatedInventoryResponse()
            {
                CategorySummaries = categories
            };
        }
        private static string NormalizeCategory(string? rawCategory)
        {
            if (string.IsNullOrWhiteSpace(rawCategory))
            {
                return "Uncategorized";
            }

            // 1. Trim leading/trailing whitespace and collapse any accidental double spaces inside
            var cleaned = string.Join(' ', rawCategory.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

            // 2. Lowercase first so ALL-CAPS words like "ELECTRONICS" get properly Title-Cased
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(cleaned.ToLowerInvariant());
        }

    }
}