using SoftwareFactory.Api.Composition;
using SoftwareFactory.Api.Middleware;
using SoftwareFactory.Api.Observability;
using SoftwareFactory.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Local, git-ignored settings (connection strings, seed password, JWT secret) for whoever does not use Aspire or
// user-secrets. They go before the environment variables, never after: see HostConfiguration.
builder.Configuration.AddLocalSettings();

builder.AddPlatformLogging();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddApiSecurity(builder.Configuration);

var app = builder.Build();

app.UsePlatformRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantClaimMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
