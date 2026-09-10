using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SoftwareFactory.Application.Common.Tenancy;
using SoftwareFactory.Infrastructure.Persistence.Security;

namespace SoftwareFactory.Infrastructure.Persistence.Tenancy;

/// <summary>
/// Pushes the current tenant into the database session every time a connection is opened, so the RLS policies see it.
/// Npgsql resets session state when a connection returns to the pool, so a tenant never leaks between scopes.
/// </summary>
public sealed class TenantConnectionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    private const string SetTenantSql = $"SELECT set_config('{RowLevelSecurity.TenantSetting}', @tenant, false)";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        var command = CreateCommand(connection);

        await using (command.ConfigureAwait(false))
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var command = connection.CreateCommand();
        command.CommandText = SetTenantSql;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant";
        parameter.Value = tenantContext.TenantId?.ToString() ?? string.Empty;
        command.Parameters.Add(parameter);

        return command;
    }
}
