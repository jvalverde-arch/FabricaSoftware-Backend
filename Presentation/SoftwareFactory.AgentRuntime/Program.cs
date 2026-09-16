using Serilog;
using SoftwareFactory.AgentRuntime.Composition;
using SoftwareFactory.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Structured logging with Serilog (estandar-backend.md §3); the job consumer of T-007 logs through it.
builder.Logging.ClearProviders();
builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", builder.Environment.ApplicationName));

// Local, git-ignored settings (connection strings, LLM keys) for whoever does not use Aspire. They go before the
// environment variables, never after: see HostConfiguration.
builder.Configuration.AddLocalSettings();

builder.Services.AddHealthChecks();
builder.Services.AddWorkerServices(builder.Configuration);

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
