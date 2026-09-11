using Microsoft.EntityFrameworkCore;
using Npgsql;
using SoftwareFactory.Infrastructure.Persistence;
using SoftwareFactory.Infrastructure.Persistence.Initialization;
using SoftwareFactory.Infrastructure.Persistence.Tenancy;
using Testcontainers.PostgreSql;

namespace SoftwareFactory.Infrastructure.Tests.Persistence;

/// <summary>
/// One PostgreSQL 17 + pgvector container per test collection, migrated with the owner account and with the application
/// login role provisioned exactly as the Development initializer does.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string AppRoleName = "sf_test_app";
    private const string AppRolePassword = "sf-test-app-password";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    /// <summary>Superuser connection: bypasses RLS, used to arrange data and inspect the catalog.</summary>
    public string AdminConnectionString { get; private set; } = string.Empty;

    /// <summary>Application role connection: subject to RLS, the one the platform uses at runtime.</summary>
    public string AppConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        AdminConnectionString = _container.GetConnectionString();

        await using var context = CreateAdminContext();
        await context.Database.MigrateAsync();
        await AppRoleProvisioner.EnsureLoginRoleAsync(context, AppRoleName, AppRolePassword, CancellationToken.None);

        AppConnectionString = new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Username = AppRoleName,
            Password = AppRolePassword,
        }.ConnectionString;
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public SoftwareFactoryDbContext CreateAdminContext() =>
        new(SoftwareFactoryDbContextOptions.Create(AdminConnectionString));

    public SoftwareFactoryDbContext CreateAppContext(Guid? tenantId)
    {
        var builder = new DbContextOptionsBuilder<SoftwareFactoryDbContext>();
        SoftwareFactoryDbContextOptions.Configure(builder, AppConnectionString);
        builder.AddInterceptors(new TenantConnectionInterceptor(new FixedTenantContext(tenantId)));
        return new SoftwareFactoryDbContext(builder.Options);
    }
}
