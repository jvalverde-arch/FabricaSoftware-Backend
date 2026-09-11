using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using SoftwareFactory.Api.ErrorHandling;
using SoftwareFactory.Api.Resources;
using SoftwareFactory.Api.Security;
using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Infrastructure.Security.Jwt;

namespace SoftwareFactory.Api.Composition;

/// <summary>Bearer authentication, role policies, rate limiting on /api/auth, CORS, ProblemDetails and localized messages.</summary>
internal static class SecurityRegistration
{
    private const string CorsOriginsKey = "Cors:AllowedOrigins";

    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLocalization();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddScoped<IClientContext, HttpClientContext>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((bearer, jwt, clock) =>
            {
                bearer.TokenValidationParameters = JwtTokenValidation.CreateParameters(jwt.Value, clock);
                bearer.MapInboundClaims = false;
            });

        services.AddAuthorization(AuthorizationPolicies.Configure);

        services.AddOptions<AuthRateLimitOptions>().Bind(configuration.GetSection(AuthRateLimitOptions.SectionName));
        services.AddRateLimiter(ConfigureRateLimiter);

        var origins = configuration.GetSection(CorsOriginsKey).Get<string[]>() ?? [];
        services.AddCors(cors => cors.AddDefaultPolicy(policy =>
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

        services.AddProblemDetails(options => options.CustomizeProblemDetails = CustomizeProblemDetails);
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = InvalidModelState);

        return services;
    }

    /// <summary>Trace id on every problem, and Spanish titles where the framework would put the English reason phrase.</summary>
    private static void CustomizeProblemDetails(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        var status = problem.Status ?? context.HttpContext.Response.StatusCode;
        var isDefaultTitle = problem.Title is null || string.Equals(problem.Title, ReasonPhrases.GetReasonPhrase(status), StringComparison.Ordinal);

        if (isDefaultTitle)
        {
            var messages = context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<ApiMessages>>();
            var localized = messages[$"Status{status}Title"];
            problem.Title = localized.ResourceNotFound ? messages["Status500Title"] : localized;
        }
    }

    private static void ConfigureRateLimiter(RateLimiterOptions limiter)
    {
        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        limiter.AddPolicy(RateLimitPolicies.Auth, context =>
        {
            var settings = context.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
            var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.PermitLimit,
                Window = settings.Window,
                QueueLimit = 0,
                AutoReplenishment = true,
            });
        });

        limiter.OnRejected = async (context, cancellationToken) =>
        {
            var services = context.HttpContext.RequestServices;
            var messages = services.GetRequiredService<IStringLocalizer<ApiMessages>>();
            var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait)
                ? wait
                : services.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value.Window;
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

            await services.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context.HttpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = messages["TooManyRequestsTitle"],
                    Detail = messages["TooManyRequests"],
                },
            });
        };
    }

    private static BadRequestObjectResult InvalidModelState(ActionContext context)
    {
        var messages = context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<ApiMessages>>();

        return new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = messages["ValidationFailedTitle"],
        })
        {
            ContentTypes = { "application/problem+json" },
        };
    }
}
