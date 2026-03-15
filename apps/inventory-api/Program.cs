using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

var serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "inventory-api";
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4318";
var logFilePath = Environment.GetEnvironmentVariable("LOG_FILE_PATH") ?? "/tmp/inventory-api.log";

Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithSpan()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .WriteTo.File(
        new RenderedCompactJsonFormatter(),
        logFilePath,
        rollingInterval: RollingInterval.Day,
        shared: true)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri($"{otlpEndpoint}/v1/traces");
            o.Protocol = OtlpExportProtocol.HttpProtobuf;
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("OnCallLab.InventoryApi")
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri($"{otlpEndpoint}/v1/metrics");
            o.Protocol = OtlpExportProtocol.HttpProtobuf;
        }));

var meter = new Meter("OnCallLab.InventoryApi");
var inventoryCounter = meter.CreateCounter<int>("inventory_checks_total");
var inventoryFailureCounter = meter.CreateCounter<int>("inventory_failures_total");

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = serviceName }));

app.MapGet("/inventory/{itemId:int}", async (int itemId, ILogger<Program> logger) =>
{
    await MaybeInjectChaosAsync(logger, serviceName, inventoryFailureCounter);

    inventoryCounter.Add(1);
    logger.LogInformation("Inventory checked for item {ItemId}", itemId);

    return Results.Ok(new
    {
        itemId,
        available = itemId % 5 != 0,
        warehouse = "AMS-1",
        traceId = Activity.Current?.TraceId.ToString()
    });
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

static async Task MaybeInjectChaosAsync(
    Microsoft.Extensions.Logging.ILogger logger,
    string serviceName,
    Counter<int> failureCounter)
{
    var chaosMode = (Environment.GetEnvironmentVariable("CHAOS_MODE") ?? "off").ToLowerInvariant();
    var failRate = double.TryParse(Environment.GetEnvironmentVariable("CHAOS_FAIL_RATE"), out var fr) ? fr : 0.0;
    var delayMs = int.TryParse(Environment.GetEnvironmentVariable("CHAOS_DELAY_MS"), out var dm) ? dm : 0;

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