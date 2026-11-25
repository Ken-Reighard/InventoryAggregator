using InventoryAggregator.Api.Models;
using InventoryAggregator.Api.Services;
using InventoryAggregator.Core.Interfaces;
using InventoryAggregator.Core.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// 1. Bind & validate configuration (appsettings.json + User Secrets / Env Vars)
builder.Services
    .AddOptions<UpstreamInventoryApiOptions>()
    .BindConfiguration(UpstreamInventoryApiOptions.SectionName)
    .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _), "UpstreamInventoryApi:BaseUrl must be a valid absolute URL.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "UpstreamInventoryApi:ApiKey is required (set via User Secrets or environment variable).")
    .Validate(o => o.PageSize is > 0 and <= 25, "UpstreamInventoryApi:PageSize must be between 1 and 25.")
    .ValidateOnStart();

// 2. Register Core aggregation service
builder.Services.AddScoped<IInventoryAggregationService, InventoryAggregationService>();

// 3. Register typed HttpClient via IHttpClientFactory
builder.Services.AddHttpClient<IInventoryClient, HttpInventoryClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<UpstreamInventoryApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
    client.Timeout = TimeSpan.FromSeconds(15);
});


builder.Services.AddControllers();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
