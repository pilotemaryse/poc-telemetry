using DockerCrudDemo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing connection string 'Default'.");

var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMq:User"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMq:Password"] ?? "guest";

var serviceName = "api";
var resource = ResourceBuilder.CreateDefault().AddService(serviceName);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddOpenTelemetry(o =>
{
    o.IncludeFormattedMessage = true;
    o.IncludeScopes = true;
    o.ParseStateValues = true;
    o.SetResourceBuilder(resource);
    o.AddOtlpExporter();
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("Microsoft.EntityFrameworkCore")
        .AddSource("MassTransit")
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter()
        // Endpoint Prometheus /metrics scrappé par l'OTel Collector -> signal `up`
        .AddPrometheusExporter());

builder.Services.AddInfrastructure(connectionString, rabbitHost, rabbitUser, rabbitPass);
builder.Services.AddControllers();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var attempts = 0;
    while (true)
    {
        try
        {
            db.Database.EnsureCreated();
            break;
        }
        catch (Exception ex) when (attempts++ < 20)
        {
            logger.LogWarning("DB not ready (attempt {Attempt}): {Message}", attempts, ex.Message);
            await Task.Delay(2000);
        }
    }
}

app.UseCors();
app.MapControllers();

// Liveness probe + endpoint de scrape Prometheus (alimente l'alerte "Service down").
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPrometheusScrapingEndpoint();

app.Run();
