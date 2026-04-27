using Dok.Api.Extensions;
using Dok.Application;
using Dok.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Host configuration ---
builder.AddDokSerilog();
builder.ConfigureDokKestrel();

// --- Services ---
builder.Services
    .AddDokTimeProvider(builder.Configuration)
    .AddDokApplication()
    .AddDokInfrastructure(builder.Configuration)
    .AddDokJson()
    .AddDokErrorHandling()
    .AddDokOpenApi();

builder.Services.AddHealthChecks();

var app = builder.Build();

// --- Middleware pipeline ---
app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseProviderHeader();

// --- Endpoints ---
app.MapDokOpenApi();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapControllers();

await app.RunAsync();

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program;
