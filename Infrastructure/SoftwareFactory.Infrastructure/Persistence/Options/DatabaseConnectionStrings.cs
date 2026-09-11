using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace SoftwareFactory.Infrastructure.Persistence.Options;

/// <summary>
/// Resolves the two connections the platform uses: <c>ConnectionStrings:softwarefactory-admin</c> (owner account: migrations,
/// role provisioning and seed, Development only) and <c>ConnectionStrings:softwarefactory</c> (application login role, RLS enforced).
/// When the application connection string is not given it is derived from the admin one with the application role credentials.
/// </summary>
public sealed class DatabaseConnectionStrings
{
    public const string AdminName = "softwarefactory-admin";
    public const string ApplicationName = "softwarefactory";

    public DatabaseConnectionStrings(IConfiguration configuration, IOptions<DatabaseOptions> options)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);

        Admin = configuration.GetConnectionString(AdminName);
        var explicitApplication = configuration.GetConnectionString(ApplicationName);

        if (Admin is null && explicitApplication is null)
        {
            throw new InvalidOperationException(
                $"No database is configured. Provide ConnectionStrings:{ApplicationName} or, in Development, ConnectionStrings:{AdminName}.");
        }

        AppRoleName = options.Value.AppRoleName;
        AppRolePassword = options.Value.AppRolePassword ?? RandomNumberGenerator.GetHexString(48, lowercase: true);
        Application = explicitApplication ?? DeriveApplication(Admin!, AppRoleName, AppRolePassword);
    }

    /// <summary>Owner connection; null outside Development where no migrations or seeds run in-process.</summary>
    public string? Admin { get; }

    /// <summary>Connection used by the DbContext at runtime.</summary>
    public string Application { get; }

    public string AppRoleName { get; }

    public string AppRolePassword { get; }

    private static string DeriveApplication(string admin, string roleName, string rolePassword)
    {
        var builder = new NpgsqlConnectionStringBuilder(admin)
        {
            Username = roleName,
            Password = rolePassword,
        };

        return builder.ConnectionString;
    }
}
