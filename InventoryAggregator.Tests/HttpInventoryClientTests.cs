using InventoryAggregator.Api.Clients;
using InventoryAggregator.Api.Models;
using InventoryAggregator.Api.Services;
using InventoryAggregator.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace InventoryAggregator.Tests
{
    public class HttpInventoryClientTests
    {

        [Fact]
        public async Task HttpInventoryClient_Automatically_Recovers()
        {
            // Arrange
            var handler = new StubHttpMessageHandler((_, attempt) =>
            {
                // Call #1: Simulate a temporary 503 server error
                if (attempt == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                }

                // Call #2: Simulate a 429 rate-limit response with a 10ms Retry-After header
                if (attempt == 2)
                {
                    var rateLimited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    rateLimited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(10));
                    return rateLimited;
                }

                // Call #3: Simulate the upstream server recovering with a 200 OK JSON page
                return JsonResponse(new UpstreamRecordsPageResponse
                {
                    Page = 1,
                    PageSize = 25,
                    TotalPages = 1,
                    TotalRecords = 1,
                    Records =
                    [
                        new InventoryRecord { Id = 1, Name = "Cordless Drill", Category = "Tools", Price = 99m, Quantity = 4 }
                    ]
                });
            });

            var client = CreateClient(handler);

            // Act
            var records = await client.GetAllRecordsAsync();

            // Assert
            Assert.Equal(3, handler.CallCount);
            var record = Assert.Single(records);
            Assert.Equal(1, record.Id);
        }

        [Fact]
        public async Task GetAllRecordsAsync_FetchesAllPages_AndDeduplicatesOverlappingRecords()
        {
            // Arrange
            var handler = new StubHttpMessageHandler((request, _) =>
            {
                if (request.RequestUri!.Query.Contains("page=1"))
                {
                    return JsonResponse(new UpstreamRecordsPageResponse
                    {
                        Page = 1,
                        PageSize = 2,
                        TotalPages = 2,
                        TotalRecords = 3,
                        Records =
                        [
                            new InventoryRecord { Id = 1, Name = "Cordless Drill", Category = "Tools", Price = 99m, Quantity = 4 },
                            new InventoryRecord { Id = 2, Name = "Corded Drill", Category = "Tools", Price = 79m, Quantity = 6 }
                        ]
                    });
                }

                return JsonResponse(new UpstreamRecordsPageResponse
                {
                    Page = 2,
                    PageSize = 2,
                    TotalPages = 2,
                    TotalRecords = 3,
                    Records =
                    [
                         new InventoryRecord { Id = 2, Name = "Corded Drill", Category = "Tools", Price = 79m, Quantity = 6 },
                         new InventoryRecord { Id = 3, Name = "Corded Saw", Category = "Tools", Price = 59m, Quantity = 10 }
                    ]
                });
            });

            var client = CreateClient(handler);

            // Act
            var records = await client.GetAllRecordsAsync();

            // Assert
           Assert.Equal(2, handler.CallCount);
           Assert.Equal(3, records.Count);
        }

        [Fact]
        public async Task GetAllRecordsAsync_Completeness_Mismatch()
        {
            // Arrange
            var handler = new StubHttpMessageHandler((request, _) =>
            {
               
                    return JsonResponse(new UpstreamRecordsPageResponse
                    {
                        Page = 1,
                        PageSize = 5,
                        TotalPages = 1,
                        TotalRecords = 5,
                        Records =
                        [
                            new InventoryRecord { Id = 2, Name = "Corded Drill", Category = "Tools", Price = 79m, Quantity = 6 }
                        ]
                    });
                
            });

            var client = CreateClient(handler);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAllRecordsAsync());
            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task Unathorized_FailFast()
        {
            // Arrange
            var handler = new StubHttpMessageHandler((_, _) =>
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            });

            var client = CreateClient(handler);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAllRecordsAsync());
            Assert.Equal(1, handler.CallCount);

        }

        [Fact]
        public async Task GetAllRecordsAsync_ThrowsJsonExceptionImmediately_WhenRecordFieldIsMalformed()
        {
            // Arrange: "price" is a string ("not-a-number") instead of a valid decimal
            var malformedJson = """
        {
          "records": [
            {
              "id": 1,
              "name": "Cordless Drill",
              "category": "Tools",
              "price": "not-a-number",
              "quantity": 4,
              "lastUpdated": "2026-09-20T01:10:00+00:00"
            }
          ],
          "page": 1,
          "pageSize": 25,
          "totalRecords": 1,
          "totalPages": 1
        }
        """;

            var handler = new StubHttpMessageHandler((_, _) =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(malformedJson, Encoding.UTF8, "application/json")
                });

            var client = CreateClient(handler);

            // Act & Assert
            await Assert.ThrowsAsync<JsonException>(() => client.GetAllRecordsAsync());
            Assert.Equal(1, handler.CallCount);
        }

        private static HttpInventoryClient CreateClient(HttpMessageHandler handler, int pageSize = 2)
        {
            // 1. Wrap your StubHttpMessageHandler inside a real HttpClient
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://prologmobile.com")
            };

            // 2. Wrap test settings in Options.Create(...) instead of appsettings/User Secrets
            var options = Options.Create(new UpstreamInventoryApiOptions
            {
                BaseUrl = "https://prologmobile.com",
                ApiKey = "test-key",
                PageSize = pageSize
            });

            // 3. Pass a no-op logger (NullLogger) and return your real HttpInventoryClient
            return new HttpInventoryClient(httpClient, options, NullLogger<HttpInventoryClient>.Instance);
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _handler;
            public int CallCount { get; private set; }

            public StubHttpMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(_handler(request, CallCount));
            }
        }

        private static HttpResponseMessage JsonResponse(UpstreamRecordsPageResponse payload) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json")
            };
    }
}
