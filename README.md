# Inventory Aggregation Service

A `.NET 10` Web API that fetches paginated inventory records from the upstream Inventory API, validates dataset completeness, aggregates available stock by normalized category, and exposes a single summary endpoint (`GET /api/inventory/summary`).

---

## Setup & Configuration

### Prerequisites
- [.NET 10 SDK](https://microsoft.com)

### Configure the Upstream API Key
The API key is intentionally excluded from `appsettings.json` and source control. Configure it using either **.NET User Secrets** or an **environment variable** before running the application:

**Option A — .NET User Secrets (Recommended for local development):**
```powershell
dotnet user-secrets set "UpstreamInventoryApi:ApiKey" "<your-api-key>" --project InventoryAggregator.Api
```

**Option B — Environment Variable (Works across Windows, macOS, and Linux):**
```powershell
# Windows PowerShell
$env:UpstreamInventoryApi__ApiKey="<your-api-key>"

# Windows Command Prompt (cmd.exe)
set UpstreamInventoryApi__ApiKey=<your-api-key>

# Bash / macOS / Linux
export UpstreamInventoryApi__ApiKey="<your-api-key>"
```

*(Optional settings in `InventoryAggregator.Api/appsettings.json`: `UpstreamInventoryApi:BaseUrl` defaults to `https://prologmobile.com` and `UpstreamInventoryApi:PageSize` defaults to `25`.)*

---

## Running the Application & Tests

### Run the API
From the solution root directory (`InventoryAggregator`), run using the `https` launch profile (recommended) or `http` launch profile from `launchSettings.json`:

```powershell
# Run with HTTPS + HTTP (recommended)
dotnet run --project InventoryAggregator.Api --launch-profile https

# Or run HTTP-only if you prefer not to trust the local HTTPS dev certificate
dotnet run --project InventoryAggregator.Api --launch-profile http
```

*(Note: If running locally over HTTPS for the first time on your machine, you may need to trust the local .NET development SSL certificate once by running `dotnet dev-certs https --trust`.)*

Once running, check the terminal output for the listening URL (`https://localhost:<port>` or `http://localhost:<port>`) and call:
```http
GET https://localhost:<port>/api/inventory/summary
```

### Sample Response (`200 OK`)

```json
{
  "categories": [
    {
      "category": "Electronics",
      "totalInventoryValue": 18253.18,
      "availableRecordCount": 25,
      "mostRecentlyUpdated": {
        "id": 1037,
        "name": "Barcode Scanner MK-1037",
        "lastUpdated": "2026-09-23T17:05:00+00:00"
      },
      "items": [
        {
          "id": 1016,
          "name": "Label Printer MK-1016",
          "price": 53.74,
          "quantity": 14,
          "lastUpdated": "2026-09-20T01:10:00+00:00"
        },
        {
          "id": 1037,
          "name": "Barcode Scanner MK-1037",
          "price": 21.99,
          "quantity": 23,
          "lastUpdated": "2026-09-23T17:05:00+00:00"
        }
      ]
    },
    {
      "category": "Home & Kitchen",
      "totalInventoryValue": 17151.51,
      "availableRecordCount": 27,
      "mostRecentlyUpdated": {
        "id": 1019,
        "name": "Electric Kettle MK-1019",
        "lastUpdated": "2026-09-23T20:36:00+00:00"
      },
      "items": [
        {
          "id": 1002,
          "name": "Espresso Machine MK-1002",
          "price": 8.24,
          "quantity": 8,
          "lastUpdated": "2026-09-09T22:31:00+00:00"
        }
      ]
    }
  ]
}
```
*(Items arrays trimmed for brevity; full output includes all 5 normalized categories: `Electronics`, `Home & Kitchen`, `Office Supplies`, `Outdoor`, and `Toys`.)*

### Run the Unit Tests
From the solution root directory:
```powershell
dotnet test
```
This runs all **9 xUnit tests** covering both the core aggregation rules (`InventoryAggregationServiceTests`) and HTTP client resilience/pagination (`HttpInventoryClientTests`).

---

## Architecture Overview

The solution is organized into three projects with strict one-way dependencies (`Api` -> `Core`, `Tests` -> `Core` & `Api`) to enforce a clean separation of concerns:

```text
InventoryAggregator.Api   ──►  InventoryAggregator.Core
         ▲                               ▲
         │                               │
         └──── InventoryAggregator.Tests ┘
```

### 1. `InventoryAggregator.Core` (Domain Models & Aggregation Business Logic)
- **What's inside:** `InventoryAggregationService`, domain/response models (`InventoryRecord`, `AggregatedInventoryResponse`, `CategorySummary`, `CategoryItem`, `MostRecentlyUpdatedItem`), and the `IInventoryClient` abstraction.
- **Why it's separated:** The aggregation engine has zero knowledge of HTTP, pagination envelopes, status codes, or configuration. Because `Core` depends only on the `IInventoryClient` interface (`GetAllRecordsAsync`), the business logic can be unit-tested in isolation without network calls and remains decoupled from how upstream data is transported.

### 2. `InventoryAggregator.Api` (HTTP Client, DI, Configuration & Web Endpoint)
- **Controller-Based Endpoint (`InventoryController`):** I chose traditional ASP.NET Core MVC Controllers (`ControllerBase` with `[ApiController]`) over Minimal APIs because I am more experienced with controller conventions, attribute routing, explicit `[ProducesResponseType]` metadata, and keeping HTTP error-to-`ProblemDetails` mapping cleanly separated from startup wiring in `Program.cs`.
- **Typed `HttpClient` via `IHttpClientFactory` (`HttpInventoryClient`):** Encapsulates all upstream API details (`api/records` pagination, `X-Api-Key` header, `Retry-After` handling, and exponential backoff) behind `IInventoryClient`. Using a typed client managed by `IHttpClientFactory` avoids socket exhaustion and stale DNS issues while keeping `Program.cs` clean and making `HttpInventoryClient` easy to test with a custom `HttpMessageHandler`.
- **Fail-Fast Configuration (`UpstreamInventoryApiOptions`):** Uses the Options pattern with `.ValidateOnStart()` so missing or invalid configuration (such as an omitted `ApiKey` or an out-of-range `PageSize`) fails immediately at startup rather than surfacing mid-request.

### 3. `InventoryAggregator.Tests` (xUnit Test Suite)
- **Why two test styles are used:**
  - `InventoryAggregationServiceTests` injects an in-memory `FakeInventoryClient` to verify category normalization, out-of-stock filtering, valuation math, and timestamp tie-breaking without touching HTTP.
  - `HttpInventoryClientTests` injects a custom `StubHttpMessageHandler` into a real `HttpClient` to verify transient `503`/`429` retry recovery, multi-page deduplication, `TotalRecords` completeness checks, malformed JSON handling, and `401` fail-fast behavior deterministically in milliseconds.

---

## Assumptions, Decisions & Known Limitations

### Decisions & Assumptions
- **Category Normalization:** Raw category names are trimmed, internal consecutive whitespace is collapsed, and strings are converted to invariant Title Case (`"  electronics "` and `"ELECTRONICS"` both normalize to `"Electronics"`). Null or blank categories map to `"Uncategorized"`.
- **Product Name & Price Assumptions:** Upstream product `Name` values are assumed to be already clean and are preserved as-is without whitespace or casing normalization. Upstream `Price` values are assumed to be valid positive numbers (`decimal`), so negative-price validation is not applied.
- **Out-of-Stock Filtering (`Quantity > 0`):** Only records with `Quantity > 0` are included; zero-quantity (and any non-positive) items are excluded from `totalInventoryValue`, `availableRecordCount`, `mostRecentlyUpdated`, and `items`.
- **Cross-Page Deduplication & Completeness:** Records are stored in a dictionary keyed by `Id` (`recordsById[record.Id] = record`) to deduplicate overlapping records across page boundaries. Once all pages are fetched, the unique record count is verified against `totalRecords` from page 1; mismatches throw `InvalidOperationException` (`502 Bad Gateway`) rather than returning incomplete totals.
- **Deterministic Ordering & Tie-Breaking:** Output categories are sorted alphabetically (`OrdinalIgnoreCase`), items within each category are sorted ascending by `Id`, and `mostRecentlyUpdated` breaks identical `LastUpdated` timestamps by highest `Id`.
- **Transient Retry & Rate-Limit Backoff:** `HttpInventoryClient` retries `429 Too Many Requests`, `5xx` server errors, `408 Request Timeout`, and transient network drops up to 4 attempts—honoring the upstream `Retry-After` header when present and falling back to exponential backoff with jitter—while failing immediately on permanent `4xx` responses (`401`, `403`, `404`) and malformed JSON payloads.

### Known Limitations & Next Steps With More Time
- **No Endpoint Authentication / Authorization:** Per the assignment specification, `GET /api/inventory/summary` is exposed without inbound authentication. In a production deployment, this endpoint would be secured (such as JWT bearer authentication or API key middleware) and paired with rate limiting.
- **No Response Caching:** Every call to `GET /api/inventory/summary` currently fetches all upstream pages fresh. Adding a short in-memory cache (for example, caching results for 15–30 seconds) would speed up repeated requests and reduce the chance of hitting upstream rate limits.
- **Sequential Page Fetching:** Pages `2..totalPages` are fetched one at a time in a loop. Fetching pages concurrently with a cap on simultaneous requests would improve response times on larger datasets, though sequential fetching was kept here to avoid triggering the staging server's rate limiter.
- **Unit Tests Only (No Full HTTP Pipeline Tests):** The current test suite focuses on unit-testing `InventoryAggregationService` and `HttpInventoryClient` in isolation. Adding full end-to-end API tests against the running controller and middleware would verify routing, DI wiring, and HTTP error status mapping automatically.
