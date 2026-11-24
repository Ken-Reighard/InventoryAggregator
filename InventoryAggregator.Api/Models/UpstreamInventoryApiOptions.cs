namespace InventoryAggregator.Api.Models
{
    public class UpstreamInventoryApiOptions 
    {
        public const string SectionName = "UpstreamInventoryApi";

        public string BaseUrl { get; set; } = string.Empty;
    
        public string ApiKey { get; set; } = string.Empty;
        public int PageSize { get; set; } = 25;
    }
}
