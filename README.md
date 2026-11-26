Inventory Aggregation
# Inventory Summary Service

An ASP.NET Core (.NET 10) web API service that fetches paginated product inventory from the upstream Inventory API, deduplicates records across page boundaries, normalizes categories, excludes out-of-stock items, and exposes a single aggregated summary endpoint at `GET /api/inventory/summary`.

---

## Prerequisites

- [.NET 10 SDK](https://microsoft.com)
- Valid `X-API-Key` for the upstream Inventory API (`https://prologmobile.com`)

---

## Setup & Configuration

The upstream API key is **not** committed to source control or stored in `appsettings.json`. Configure it locally via **.NET User Secrets** (recommended for local development) or an **environment variable**.

### Option 1: .NET User Secrets (Recommended)
From the repository root:

```bash
dotnet user-secrets set "UpstreamInventoryApi:ApiKey" "YOUR_API_KEY" --project src/InventorySummary.Api
```

### Option 2: Environment Variable
Set `UpstreamInventoryApi__ApiKey` (note the double underscore `__`) in your shell before running the app:

**PowerShell:**
```powershell
$env:UpstreamInventoryApi__ApiKey="YOUR_API_KEY"
```

**Bash / macOS / Linux:**
```bash
export UpstreamInventoryApi__ApiKey="YOUR_API_KEY"
```

> **Startup Validation:** `UpstreamInventoryApiOptions` is validated on application startup (`.ValidateOnStart()`). If `UpstreamInventoryApi:ApiKey` is missing or blank, the application fails fast on startup with a clear configuration error rather than failing at request time.

---

## Running the Application

1. *(Optional, first-time HTTPS setup)* Trust the local ASP.NET Core development certificate:
   ```bash
   dotnet dev-certs https --trust
   ```

2. Run the API project from the repository root:
   ```bash
   dotnet run --project src/InventorySummary.Api --launch-profile https
   ```
   *(Or use `--launch-profile http` to run over HTTP on `http://localhost:5222`.)*

3. Query the summary endpoint:
   ```bash
   curl -k https://localhost:7222/api/inventory/summary
   ```

### Sample Response (`200 OK`)
```json
{
  "totalInventoryValue": 14820.50,
  "availableRecordCount": 18,
  "mostRecentlyUpdated": {
    "id": 42,
    "name": "Wireless Barcode Scanner",
    "category": "Electronics",
    "price": 129.99,
    "quantity": 12,
    "lastUpdated": "2026-03-14T18:22:10Z"
  },
  "categories": [
    {
      "category": "Electronics",
      "totalValue": 9450.00,
      "items": [
        {
          "id": 10,
          "name": "Rugged Handheld Terminal",
          "price": 850.00,
          "quantity": 10,
          "lastUpdated": "2026-03-10T12:00:00Z"
        }
      ]
    }
  ]
}
```

---

## Running the Unit Tests

Run all unit tests from the repository root:

```bash
dotnet test
```

The test suite (`tests/InventorySummary.Tests`) contains **9 xUnit tests** covering:
- Category normalization (trimming, collapsing internal whitespace, Title Case conversion, and `"Uncategorized"` fallback for null/whitespace values)
- Filtering out zero-quantity items (`Quantity == 0`) from totals, counts, `mostRecentlyUpdated`, and category item lists
- Empty inventory handling (when all items are out of stock or the upstream API returns zero records)
- Multi-page aggregation, cross-page duplicate ID deduplication, and `totalRecords` mismatch detection
- Deterministic category sorting (alphabetical), item sorting (ascending by `Id`), and `mostRecentlyUpdated` tie-breaking (highest `Id`)
- `HttpInventoryClient` HTTP behavior: `X-API-Key` header injection, `429 Too Many Requests` retry honoring `Retry-After`, immediate failure without retries on `401 Unauthorized`, and clean error handling on malformed JSON payloads

---

## Architecture Overview

The solution is organized into three projects with strict one-way dependencies (`Api` → `Core`, and `Tests` → `Core` & `Api`):

```text
InventorySummary.slnx
├── src/
│   ├── InventorySummary.Core/        # Pure domain models, interfaces, and business logic (no ASP.NET dependencies)
│   │   ├── Models/                   # Upstream DTOs & API response DTOs
│   │   └── Services/                 # IUpstreamInventoryClient & InventoryAggregationService
│   └── InventorySummary.Api/         # ASP.NET Core host, HTTP client, controller, and error mapping
│       ├── Configuration/            # Strongly-typed UpstreamInventoryApiOptions
│       ├── Controllers/              # InventoryController (GET /api/inventory/summary)
│       └── Services/                 # HttpInventoryClient (pagination, headers, transient retry)
└── tests/
    └── InventorySummary.Tests/       # xUnit unit tests for aggregation logic and HTTP client behavior
```

### Key Design Choices
- **Clean Separation of `Core` and `Api`:** All aggregation, deduplication, normalization, and sorting rules live in `InventorySummary.Core` with zero dependency on HTTP or ASP.NET Core, making the business rules fast and straightforward to unit-test in isolation.
- **MVC Controller (`InventoryController`):** Uses standard ASP.NET Core controller routing (`[ApiController]`) paired with `ProblemDetails` (`AddProblemDetails()`) so upstream failures (`502 Bad Gateway`), timeouts (`504 Gateway Timeout`), and unexpected server errors (`500 Internal Server Error`) return consistent RFC 7807 error payloads.
- **Typed `HttpClient` via `IHttpClientFactory`:** `HttpInventoryClient` is registered via `AddHttpClient<IUpstreamInventoryClient, HttpInventoryClient>()` to avoid socket exhaustion and centralize base URL, timeout, `X-API-Key` header injection, and transient retry handling.
- **Isolated HTTP & Aggregation Testing:** Aggregation tests use a lightweight fake `IUpstreamInventoryClient`, while HTTP client tests inject a custom `HttpMessageHandler` into `HttpClient` to verify retry backoff, header forwarding, and malformed JSON handling without making live network calls.

---

## Decisions & Assumptions

- **Category Normalization:** Raw category names are trimmed, internal consecutive whitespace is collapsed, and strings are converted to invariant Title Case (`"  electronics "` and `"ELECTRONICS"` both normalize to `"Electronics"`). Null or blank categories map to `"Uncategorized"`.
- **Product Name & Price Assumptions:** Upstream product `Name` values are assumed to be already clean and are preserved as-is without whitespace or casing normalization. Upstream `Price` values are assumed to be valid positive numbers (`decimal`), so negative-price validation is not applied.
- **Out-of-Stock Filtering (`Quantity > 0`):** Only records with `Quantity > 0` are included; zero-quantity (and any non-positive) items are excluded from `totalInventoryValue`, `availableRecordCount`, `mostRecentlyUpdated`, and `items`.
- **Cross-Page Deduplication & Completeness:** Records are stored in a dictionary keyed by `Id` (`recordsById[record.Id] = record`) to deduplicate overlapping records across page boundaries. Once all pages are fetched, the unique record count is verified against `totalRecords` from page 1; mismatches throw `InvalidOperationException` (`502 Bad Gateway`) rather than returning incomplete totals.
- **Deterministic Ordering & Tie-Breaking:** Output categories are sorted alphabetically (`OrdinalIgnoreCase`), items within each category are sorted ascending by `Id`, and `mostRecentlyUpdated` breaks identical `LastUpdated` timestamps by highest `Id`.
- **Transient Retry & Rate-Limit Backoff:** `HttpInventoryClient` retries `429 Too Many Requests`, `5xx` server errors, `408 Request Timeout`, and transient network drops up to 4 attempts—honoring the upstream `Retry-After` header when present and falling back to exponential backoff with jitter—while failing immediately on permanent `4xx` responses (`401`, `403`, `404`) and malformed JSON payloads.

---

## Known Limitations & Next Steps With More Time

- **No Endpoint Authentication / Authorization:** Per the assignment specification, `GET /api/inventory/summary` is exposed without inbound authentication. In a production deployment, this endpoint would be secured (such as JWT bearer authentication or API key middleware) and paired with rate limiting.
- **No Response Caching:** Every call to `GET /api/inventory/summary` currently fetches all upstream pages fresh. Adding a short in-memory cache (for example, caching results for 15–30 seconds) would speed up repeated requests and reduce the chance of hitting upstream rate limits.
- **Sequential Page Fetching:** Pages `2..totalPages` are fetched one at a time in a loop. Fetching pages concurrently with a cap on simultaneous requests would improve response times on larger datasets, though sequential fetching was kept here to avoid triggering the staging server's rate limiter.
- **Unit Tests Only (No Full HTTP Pipeline Tests):** The current test suite focuses on unit-testing `InventoryAggregationService` and `HttpInventoryClient` in isolation. Adding full end-to-end API tests against the running controller and middleware would verify routing, DI wiring, and HTTP error status mapping automatically.
