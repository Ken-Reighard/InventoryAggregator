using InventoryAggregator.Core.Interfaces;
using InventoryAggregator.Core.Models;

namespace InventoryAggregator.Tests;

public class FakeInventoryClient : IInventoryClient
{
    private readonly IReadOnlyList<InventoryRecord> _records;

    public FakeInventoryClient(IReadOnlyList<InventoryRecord> records)
    {
        _records = records;
    }

    public Task<IReadOnlyList<InventoryRecord>> GetAllRecordsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_records);
}
