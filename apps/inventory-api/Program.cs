using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

var serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "inventory-api";
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4317";
var logFilePath = Environment.GetEnvironmentVariable("LOG_FILE_PATH") ?? "/tmp/inventory-api.log";
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

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("OnCallLab.InventoryApi")
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

var meter = new Meter("OnCallLab.InventoryApi");
var inventoryCounter = meter.CreateCounter<int>("inventory_checks_total");
var inventoryFailureCounter = meter.CreateCounter<int>("inventory_failures_total");

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = serviceName }));

app.MapGet("/inventory/{itemId:int}", async (int itemId, ILogger<Program> logger) =>
{
    await MaybeInjectChaosAsync(chaosMode, failRate, delayMs, logger, serviceName, inventoryFailureCounter);
    inventoryCounter.Add(1);
    logger.LogInformation("Inventory checked for item {ItemId}", itemId);
    return Results.Ok(new { itemId, available = itemId % 5 != 0, warehouse = "AMS-1", traceId = Activity.Current?.TraceId.ToString() });
});

app.MapPost("/chaos", (ChaosRequest request, ILogger<Program> logger) =>
{
    Environment.SetEnvironmentVariable("CHAOS_MODE", request.Mode);
    Environment.SetEnvironmentVariable("CHAOS_FAIL_RATE", request.FailRate.ToString("0.00"));
    Environment.SetEnvironmentVariable("CHAOS_DELAY_MS", request.DelayMs.ToString());
    logger.LogWarning("Inventory API chaos updated: {@Request}", request);
    return Results.Ok(request);
});

app.Run();

static int ParseInt(string key, int defaultValue) => int.TryParse(Environment.GetEnvironmentVariable(key), out var value) ? value : defaultValue;
static double ParseDouble(string key, double defaultValue) => double.TryParse(Environment.GetEnvironmentVariable(key), out var value) ? value : defaultValue;

static async Task MaybeInjectChaosAsync(string chaosMode, double failRate, int delayMs, Microsoft.Extensions.Logging.ILogger logger, string serviceName, Counter<int> failureCounter)
{
    if (delayMs > 0)
    {
        logger.LogWarning("{ServiceName} delaying response by {DelayMs}ms", serviceName, delayMs);
        await Task.Delay(delayMs);
    }

    if (chaosMode == "fail" || (failRate > 0 && Random.Shared.NextDouble() < failRate))
    {
        failureCounter.Add(1);
        logger.LogError("{ServiceName} chaos failure injected", serviceName);
        throw new InvalidOperationException($"Injected failure in {serviceName}");
    }
}

record ChaosRequest(string Mode, double FailRate, int DelayMs);
