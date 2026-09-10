using Microsoft.Extensions.Logging;

namespace SoftwareFactory.Infrastructure.Persistence.Initialization;

internal static partial class InitializationLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Database migrated to the latest schema.")]
    public static partial void DatabaseMigrated(this ILogger logger);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Application login role '{RoleName}' provisioned.")]
    public static partial void AppRoleProvisioned(this ILogger logger, string roleName);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Seed: tenant '{TenantSlug}' created.")]
    public static partial void TenantSeeded(this ILogger logger, string tenantSlug);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Seed: administrator '{AdminEmail}' created for tenant '{TenantSlug}'.")]
    public static partial void AdminSeeded(this ILogger logger, string adminEmail, string tenantSlug);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning, Message = "Seed: Seed:AdminPassword is not configured; the administrator was not created. Set it with user-secrets or an Aspire parameter.")]
    public static partial void AdminPasswordMissing(this ILogger logger);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Warning, Message = "Database initializer skipped: ConnectionStrings:softwarefactory-admin is not configured.")]
    public static partial void AdminConnectionMissing(this ILogger logger);
}
