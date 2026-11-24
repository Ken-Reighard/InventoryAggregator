using InventoryAggregator.Core.Models;

namespace InventoryAggregator.Api.Clients
{
    public class UpstreamRecordsPageResponse
    {
        public List<InventoryRecord> Records { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
    }
}
