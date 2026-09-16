using Serilog;
using SoftwareFactory.Application.Common.Security;
using Serilog.Events;

namespace SoftwareFactory.Api.Observability;

/// <summary>
/// Structured logging with Serilog (estandar-backend.md §3). The request log carries <c>TraceId</c> and, once the
/// token has been validated, <c>TenantId</c> and <c>UserId</c>. Never a prompt, a token or a password.
/// </summary>
internal static class SerilogRegistration
{
    public static IHostApplicationBuilder AddPlatformLogging(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((services, configuration) => configuration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", builder.Environment.ApplicationName));

        return builder;
    }

    public static IApplicationBuilder UsePlatformRequestLogging(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = static (httpContext, _, exception) =>
                exception is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError
                    ? LogEventLevel.Error
                    : httpContext.Request.Path.StartsWithSegments("/health", StringComparison.Ordinal)
                        ? LogEventLevel.Debug
                        : LogEventLevel.Information;

            options.EnrichDiagnosticContext = static (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);

                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    diagnosticContext.Set("TenantId", httpContext.User.FindFirst(AuthClaims.TenantId)?.Value);
                    diagnosticContext.Set("UserId", httpContext.User.FindFirst(AuthClaims.Subject)?.Value);
                }
            };
        });
    }
}
