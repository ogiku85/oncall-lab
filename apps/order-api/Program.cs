using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

var serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "order-api";
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4317";
var inventoryBaseUrl = Environment.GetEnvironmentVariable("INVENTORY_API_BASE_URL") ?? "http://localhost:8081";
var logFilePath = Environment.GetEnvironmentVariable("LOG_FILE_PATH") ?? "/tmp/order-api.log";
var failRate = ParseDouble("CHAOS_FAIL_RATE", 0.0);
var delayMs = ParseInt("CHAOS_DELAY_MS", 0);
var chaosMode = (Environment.GetEnvironmentVariable("CHAOS_MODE") ?? "off").ToLowerInvariant();

Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithSpan()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .WriteTo.File(new RenderedCompactJsonFormatter(), logFilePath, rollingInterval: RollingInterval.Day, shared: true)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddHttpClient("inventory", client =>
{
    client.BaseAddress = new Uri(inventoryBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("OnCallLab.OrderApi")
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

var meter = new Meter("OnCallLab.OrderApi");
var orderCounter = meter.CreateCounter<int>("orders_created_total");
var failureCounter = meter.CreateCounter<int>("orders_failed_total");

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = serviceName }));

app.MapGet("/orders/{orderId:int}", async (int orderId, IHttpClientFactory httpClientFactory, ILogger<Program> logger, HttpContext httpContext) =>
{
    await MaybeInjectChaosAsync(chaosMode, failRate, delayMs, logger, serviceName);

    var client = httpClientFactory.CreateClient("inventory");
    var response = await client.GetAsync($"/inventory/{orderId}");
    var content = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        failureCounter.Add(1);
        logger.LogError("Inventory dependency failed for order {OrderId}. StatusCode={StatusCode} Body={Body}", orderId, (int)response.StatusCode, content);
        return Results.Problem(title: "Inventory dependency failure", statusCode: (int)HttpStatusCode.BadGateway, detail: content);
    }

    orderCounter.Add(1);
    logger.LogInformation("Order {OrderId} processed successfully", orderId);
    return Results.Ok(new { orderId, status = "processed", inventory = content, traceId = Activity.Current?.TraceId.ToString() });
});

app.MapPost("/chaos", (ChaosRequest request, ILogger<Program> logger) =>
{
    Environment.SetEnvironmentVariable("CHAOS_MODE", request.Mode);
    Environment.SetEnvironmentVariable("CHAOS_FAIL_RATE", request.FailRate.ToString("0.00"));
    Environment.SetEnvironmentVariable("CHAOS_DELAY_MS", request.DelayMs.ToString());
    logger.LogWarning("Order API chaos updated: {@Request}", request);
    return Results.Ok(request);
});

app.Run();

static int ParseInt(string key, int defaultValue) => int.TryParse(Environment.GetEnvironmentVariable(key), out var value) ? value : defaultValue;
static double ParseDouble(string key, double defaultValue) => double.TryParse(Environment.GetEnvironmentVariable(key), out var value) ? value : defaultValue;

static async Task MaybeInjectChaosAsync(string chaosMode, double failRate, int delayMs, Microsoft.Extensions.Logging.ILogger logger, string serviceName)
{
    if (delayMs > 0)
    {
        logger.LogWarning("{ServiceName} delaying response by {DelayMs}ms", serviceName, delayMs);
        await Task.Delay(delayMs);
    }

    if (chaosMode == "fail" || (failRate > 0 && Random.Shared.NextDouble() < failRate))
    {
        logger.LogError("{ServiceName} chaos failure injected", serviceName);
        throw new InvalidOperationException($"Injected failure in {serviceName}");
    }
}

record ChaosRequest(string Mode, double FailRate, int DelayMs);
