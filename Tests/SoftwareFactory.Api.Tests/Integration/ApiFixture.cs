using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SoftwareFactory.AgentRuntime.Jobs;
using SoftwareFactory.Api.Tests.Integration.Probes;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Application.Platform.Handlers;
using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Persistence;
using SoftwareFactory.Infrastructure.Persistence.Options;
using Testcontainers.PostgreSql;

namespace SoftwareFactory.Api.Tests.Integration;

/// <summary>
/// The real Api host (Development: migrates, provisions the app role, seeds admin@local) over a throw-away PostgreSQL, with the
/// role probe controller added from this assembly. Users beyond the seeded admin are arranged through the owner connection.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime, IAsyncDisposable
{
    public const string AdminEmail = "admin@local";
    public const string AdminPassword = "Admin-Password-For-Tests-2026";
    public const string AllowedOrigin = "https://localhost:5173";
    public const string SigningKeyId = "test-1";
    public const string SigningSecret = "integration-test-signing-key-0123456789ABCDEF";
    public const string Issuer = "https://softwarefactory.test";
    public const string Audience = "softwarefactory-api";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();
    private WebApplicationFactory<ApiAssemblyMarker>? _baseFactory;
    private WebApplicationFactory<ApiAssemblyMarker>? _factory;

    public WebApplicationFactory<ApiAssemblyMarker> Factory => _factory ?? throw new InvalidOperationException("Fixture not initialised.");

    public string AdminConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _baseFactory = new WebApplicationFactory<ApiAssemblyMarker>();
        _factory = _baseFactory.WithWebHostBuilder(builder => Configure(builder, permitLimit: 10_000));
        using var warmup = _factory.CreateClient();
        Assert.True((await warmup.GetAsync(new Uri("/health", UriKind.Relative))).IsSuccessStatusCode);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_baseFactory is not null)
        {
            await _baseFactory.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    /// <summary>
    /// Same host with a tighter rate limit and its own limiter state, for the throttling tests. Its worker stays off:
    /// a second worker would claim jobs and hold their lease when the factory is disposed mid-run.
    /// </summary>
    public WebApplicationFactory<ApiAssemblyMarker> CreateThrottledFactory(int permitLimit) =>
        Factory.WithWebHostBuilder(builder =>
        {
            Configure(builder, permitLimit);
            builder.ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Jobs:Enabled"] = "false" }));
        });

    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsync());

    public HttpClient CreateClient() => CreateClient(Factory);

    public static HttpClient CreateClient(WebApplicationFactory<ApiAssemblyMarker> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });
        client.DefaultRequestHeaders.UserAgent.ParseAdd("xunit/1.0");
        return client;
    }

    /// <summary>Creates a user of the local tenant with the given roles; the password is hashed like the seed does.</summary>
    public async Task<AppUser> CreateUserAsync(string email, string password, params Role[] roles)
    {
        var hasher = Factory.Services.GetRequiredService<IPasswordHasher>();
        await using var context = new SoftwareFactoryDbContext(SoftwareFactoryDbContextOptions.Create(AdminConnectionString));
        var user = new AppUser(SeedOptions.LocalTenantId, email, "Usuario de prueba", hasher.Hash(password));
        context.Users.Add(user);
        context.UserRoles.AddRange(roles.Select(role => new UserRole(SeedOptions.LocalTenantId, user.Id, role)));
        await context.SaveChangesAsync();
        return user;
    }

    /// <summary>Queues a run for a different tenant, to prove one tenant cannot watch another's progress.</summary>
    public async Task<Guid> QueueForeignJobAsync()
    {
        await using var context = new SoftwareFactoryDbContext(SoftwareFactoryDbContextOptions.Create(AdminConnectionString));
        var tenant = new Tenant($"otro-{Guid.NewGuid():N}", $"otro-{Guid.NewGuid():N}");
        context.Tenants.Add(tenant);

        var job = new Job(tenant.Id, "probe", """{"steps":1}""");
        context.Jobs.Add(job);
        await context.SaveChangesAsync();

        // Parked far in the future so the worker of this host never picks it up.
        await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ExecuteUpdateAsync(context.Jobs.Where(entity => entity.Id == job.Id), setters => setters.SetProperty(entity => entity.AvailableAt, DateTimeOffset.UtcNow.AddYears(1)));

        return job.Id;
    }

    public async Task<IReadOnlyList<AuditEvent>> AuditEventsOfAsync(Guid userId)
    {
        await using var context = new SoftwareFactoryDbContext(SoftwareFactoryDbContextOptions.Create(AdminConnectionString));
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            context.AuditEvents.Where(auditEvent => auditEvent.ActorId == userId).OrderBy(auditEvent => auditEvent.OccurredAt));
    }

    private void Configure(IWebHostBuilder builder, int permitLimit)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:softwarefactory-admin"] = AdminConnectionString,
            ["Database:AppRoleName"] = "sf_api_test_app",
            ["Database:AppRolePassword"] = "sf-api-test-app-password",
            ["Seed:AdminPassword"] = AdminPassword,
            ["Argon2:MemoryKiB"] = "8192",
            ["Argon2:Iterations"] = "2",
            ["Jwt:Issuer"] = Issuer,
            ["Jwt:Audience"] = Audience,
            ["Jwt:ActiveKeyId"] = SigningKeyId,
            ["Jwt:SigningKeys:0:KeyId"] = SigningKeyId,
            ["Jwt:SigningKeys:0:Secret"] = SigningSecret,
            ["Auth:RateLimit:PermitLimit"] = permitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Auth:RateLimit:Window"] = "00:01:00",
            ["Cors:AllowedOrigins:0"] = AllowedOrigin,
            // The worker runs inside this host so the queue can be exercised end to end; in production it stays off
            // here (appsettings.json) and lives in AgentRuntime.
            ["Jobs:Enabled"] = "true",
            ["Jobs:PollInterval"] = "00:00:00.200",
            // Short lease: a worker that dies in a test must not park a job for minutes.
            ["Jobs:Lease"] = "00:00:10",
            ["Jobs:MaxAttempts"] = "2",
            ["Jobs:RetryBaseDelay"] = "00:00:01",
            ["Jobs:RetryMaxDelay"] = "00:00:02",
            ["Jobs:Stream:PollInterval"] = "00:00:00.200",
            ["Jobs:Stream:Heartbeat"] = "00:00:02",
            ["Jobs:Stream:MaxDuration"] = "00:01:00",
        }));
        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(RoleProbeController).Assembly);
            services.AddScoped<IJobHandler, ProbeJobHandler>();
            services.AddHostedService<JobWorker>();
        });
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollectionDefinition : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}
