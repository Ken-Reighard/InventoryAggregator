using InventoryAggregator.Api.Clients;
using InventoryAggregator.Api.Models;
using InventoryAggregator.Core.Interfaces;
using InventoryAggregator.Core.Models;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;

namespace InventoryAggregator.Api.Services
{
    public class HttpInventoryClient : IInventoryClient
    {
        private HttpClient _client;
        private UpstreamInventoryApiOptions _options;
        private ILogger<HttpInventoryClient> _logger;

        public HttpInventoryClient(HttpClient client, IOptions<UpstreamInventoryApiOptions> options, ILogger<HttpInventoryClient> logger)
        {
            _client = client;
            _options = options.Value;
            _logger = logger;
        }

        private async Task<UpstreamRecordsPageResponse> FetchPageAsync(int page, CancellationToken cancellationToken)
        {
            const int maxAttempts = 4;
            var url = $"api/records?page={page}&pageSize={_options.PageSize}";

            for (int attempt = 1; ; attempt++)
            {
                try
                {

                    using var response = await _client.GetAsync(
                        url,
                        cancellationToken);

                    if (((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.TooManyRequests) && attempt < maxAttempts)
                    {
                        var delay = GetRetryDelay(response, attempt);

                        _logger.LogWarning("Transient HTTP {StatusCode} on page {Page} (attempt {Attempt}/{MaxAttempts}). Retrying...",
                            (int)response.StatusCode, page, attempt, maxAttempts);
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();

                    var payload = await response.Content.ReadFromJsonAsync<UpstreamRecordsPageResponse>(cancellationToken);
                    return payload ?? throw new InvalidOperationException($"Upstream API returned an empty payload for page {page}.");
                }
                catch (HttpRequestException ex) when (attempt < maxAttempts)
                {
                    var delay = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1));

                    _logger.LogWarning(ex, "Network error fetching page {Page} (attempt {Attempt}/{MaxAttempts}). Retrying...",
                        page, attempt, maxAttempts);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
        {
            // 1. Honor the server's Retry-After header if provided (delta seconds or HTTP date)
            if (response.Headers.RetryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero)
            {
                return delta;
            }

            if (response.Headers.RetryAfter?.Date is DateTimeOffset retryDate)
            {
                var wait = retryDate - DateTimeOffset.UtcNow;
                if (wait > TimeSpan.Zero)
                {
                    return wait;
                }
            }

            // 2. For 429 without Retry-After, use exponential backoff (2s, 4s, 8s, 16s) + small jitter
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var jitterMs = Random.Shared.Next(100, 400);
                return TimeSpan.FromSeconds(Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(jitterMs);
            }

            // 3. For 5xx transient server errors, shorter exponential backoff (500ms, 1s, 2s, 4s)
            return TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1));
        }

        public async Task<IReadOnlyList<InventoryRecord>> GetAllRecordsAsync(CancellationToken cancellationToken = default)
        {
            var recordsById = new Dictionary<int, InventoryRecord>();

            var firstPage = await FetchPageAsync(1, cancellationToken);

            foreach (var record in firstPage.Records)
            {
                recordsById[record.Id] = record;
            }

            for (int page = 2; page <= firstPage.TotalPages; page++)
            {
                //await Task.Delay(TimeSpan.FromMilliseconds(5000), cancellationToken);
                var nextPage = await FetchPageAsync(page, cancellationToken);

                foreach (var record in nextPage.Records)
                {
                    recordsById[record.Id] = record;
                }
            }

            if (recordsById.Count != firstPage.TotalRecords)
            {
                throw new InvalidOperationException($"Total records found ({recordsById.Count}) do not match total records expected ({firstPage.TotalRecords}).");
            }

            return recordsById.Values.ToList();
        }
    }
}

