using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SoftwareFactory.Infrastructure.Persistence.Security;

namespace SoftwareFactory.Infrastructure.Persistence.Initialization;

/// <summary>
/// Creates or updates the login role the application connects with, as a member of the RLS-bound group role created by the
/// migration. The statements are composed by PostgreSQL itself (format %I / %L) so identifiers and passwords are quoted server-side.
/// </summary>
public static class AppRoleProvisioner
{
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "The DDL text is produced by PostgreSQL's format() with %I/%L quoting from parameters; DDL cannot take bind parameters.")]
    public static async Task EnsureLoginRoleAsync(DbContext context, string roleName, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var exists = await ScalarAsync<bool>(
                connection,
                "SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = @name)",
                cancellationToken,
                new NpgsqlParameter("name", roleName)).ConfigureAwait(false);

            var statementTemplate = exists
                ? "SELECT format('ALTER ROLE %I LOGIN PASSWORD %L', @name, @password)"
                : $"SELECT format('CREATE ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS PASSWORD %L IN ROLE {RowLevelSecurity.ApplicationRole}', @name, @password)";

            var statement = await ScalarAsync<string>(
                connection,
                statementTemplate,
                cancellationToken,
                new NpgsqlParameter("name", roleName),
                new NpgsqlParameter("password", password)).ConfigureAwait(false);

            var command = connection.CreateCommand();

            await using (command.ConfigureAwait(false))
            {
                command.CommandText = statement;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (exists)
            {
                var grant = connection.CreateCommand();

                await using (grant.ConfigureAwait(false))
                {
                    grant.CommandText = await ScalarAsync<string>(
                        connection,
                        $"SELECT format('GRANT {RowLevelSecurity.ApplicationRole} TO %I', @name)",
                        cancellationToken,
                        new NpgsqlParameter("name", roleName)).ConfigureAwait(false);
                    await grant.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            await context.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Callers pass constant SQL with bind parameters; values never reach the command text.")]
    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql, CancellationToken cancellationToken, params NpgsqlParameter[] parameters)
    {
        var command = connection.CreateCommand();

        await using (command.ConfigureAwait(false))
        {
            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (T)result!;
        }
    }
}
