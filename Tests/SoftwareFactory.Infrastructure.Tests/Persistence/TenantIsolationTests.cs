using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Persistence.Security;

namespace SoftwareFactory.Infrastructure.Tests.Persistence;

/// <summary>Acceptance criterion of T-003: a session of tenant A neither sees nor writes rows of tenant B, in every table.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class TenantIsolationTests(PostgresFixture fixture) : IAsyncLifetime
{
    private Guid _tenantA;
    private Guid _tenantB;

    public static TheoryData<string> Tables => [.. RowLevelSecurity.AllTables];

    public async Task InitializeAsync()
    {
        await using var admin = fixture.CreateAdminContext();
        _tenantA = await TenantGraph.CreateAsync(admin);
        _tenantB = await TenantGraph.CreateAsync(admin);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task Session_of_tenant_A_sees_only_its_own_rows(string table)
    {
        var tenantColumn = string.Equals(table, RowLevelSecurity.TenantTable, StringComparison.Ordinal) ? "id" : "tenant_id";

        var rowsOfA = await CountAsync(fixture.AdminConnectionString, table, tenantColumn, _tenantA, sessionTenant: null);
        var rowsOfEveryone = await CountAsync(fixture.AdminConnectionString, table, null, null, sessionTenant: null);
        Assert.True(rowsOfA > 0, $"{table}: the probe graph must have rows for tenant A");
        Assert.True(rowsOfEveryone > rowsOfA, $"{table}: the probe graph must have rows of other tenants");

        var seenByA = await CountAsync(fixture.AppConnectionString, table, null, null, sessionTenant: _tenantA);
        Assert.Equal(rowsOfA, seenByA);

        var seenWithoutTenant = await CountAsync(fixture.AppConnectionString, table, null, null, sessionTenant: null);
        Assert.Equal(0, seenWithoutTenant);
    }

    [Fact]
    public async Task Session_of_tenant_A_reads_its_own_rows_through_entity_framework()
    {
        await using var context = fixture.CreateAppContext(_tenantA);

        Assert.Equal(1, await context.Projects.CountAsync());
        Assert.Equal(2, await context.Artifacts.CountAsync());
        Assert.All(await context.Jobs.ToListAsync(), job => Assert.Equal(_tenantA, job.TenantId));

        var chunk = await context.Chunks.SingleAsync();
        Assert.Equal(TenantGraph.Embedding(0.25f).ToArray(), chunk.Embedding!.Value.ToArray());
    }

    [Fact]
    public async Task Session_of_tenant_A_cannot_insert_rows_for_tenant_B()
    {
        await using var context = fixture.CreateAppContext(_tenantA);
        context.Jobs.Add(new Job(_tenantB, "smuggled", """{"n":2}"""));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, postgres.SqlState);
    }

    [Fact]
    public async Task Session_of_tenant_A_cannot_update_or_delete_rows_of_tenant_B()
    {
        await using var context = fixture.CreateAppContext(_tenantA);

        var updated = await context.Projects
            .Where(project => project.TenantId == _tenantB)
            .ExecuteUpdateAsync(setters => setters.SetProperty(project => project.Name, "hacked"));
        var deleted = await context.Jobs
            .Where(job => job.TenantId == _tenantB)
            .ExecuteDeleteAsync();

        Assert.Equal(0, updated);
        Assert.Equal(0, deleted);

        await using var admin = fixture.CreateAdminContext();
        Assert.False(await admin.Projects.AnyAsync(project => project.Name == "hacked"));
        Assert.Equal(1, await admin.Jobs.CountAsync(job => job.TenantId == _tenantB));
    }

    [Fact]
    public async Task Every_table_has_forced_row_level_security_and_a_tenant_policy()
    {
        await using var connection = new NpgsqlConnection(fixture.AdminConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT c.relname, c.relrowsecurity, c.relforcerowsecurity,
                   (SELECT count(*) FROM pg_policies p WHERE p.tablename = c.relname AND p.policyname = @policy) AS policies
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relkind = 'r' AND c.relname = ANY(@tables)
            """,
            connection);
        command.Parameters.AddWithValue("policy", RowLevelSecurity.PolicyName);
        command.Parameters.AddWithValue("tables", RowLevelSecurity.AllTables.ToArray());

        var protectedTables = new List<string>();

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var table = reader.GetString(0);
            Assert.True(reader.GetBoolean(1), $"{table}: row level security is not enabled");
            Assert.True(reader.GetBoolean(2), $"{table}: row level security is not forced on the owner");
            Assert.Equal(1, reader.GetInt64(3));
            protectedTables.Add(table);
        }

        Assert.Equal(RowLevelSecurity.AllTables.Order(StringComparer.Ordinal), protectedTables.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Application_login_role_cannot_bypass_row_level_security()
    {
        await using var connection = new NpgsqlConnection(fixture.AdminConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT r.rolsuper, r.rolbypassrls,
                   EXISTS (SELECT 1 FROM pg_auth_members m JOIN pg_roles g ON g.oid = m.roleid
                           WHERE m.member = r.oid AND g.rolname = @groupRole) AS is_member
            FROM pg_roles r WHERE r.rolname = @role
            """,
            connection);
        command.Parameters.AddWithValue("role", PostgresFixture.AppRoleName);
        command.Parameters.AddWithValue("groupRole", RowLevelSecurity.ApplicationRole);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "the application login role must exist");
        Assert.False(reader.GetBoolean(0), "the application role must not be a superuser");
        Assert.False(reader.GetBoolean(1), "the application role must not bypass RLS");
        Assert.True(reader.GetBoolean(2), $"the application role must be a member of {RowLevelSecurity.ApplicationRole}");
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Table and column names come from the constant list of the schema; tenant ids are bound parameters.")]
    private static async Task<long> CountAsync(string connectionString, string table, string? tenantColumn, Guid? tenantFilter, Guid? sessionTenant)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var setTenant = new NpgsqlCommand($"SELECT set_config('{RowLevelSecurity.TenantSetting}', @tenant, false)", connection))
        {
            setTenant.Parameters.AddWithValue("tenant", sessionTenant?.ToString() ?? string.Empty);
            await setTenant.ExecuteNonQueryAsync();
        }

        var sql = tenantColumn is null
            ? $"SELECT count(*) FROM {table}"
            : $"SELECT count(*) FROM {table} WHERE {tenantColumn} = @filter";

        await using var count = new NpgsqlCommand(sql, connection);

        if (tenantFilter is Guid filter)
        {
            count.Parameters.AddWithValue("filter", filter);
        }

        return (long)(await count.ExecuteScalarAsync())!;
    }
}
