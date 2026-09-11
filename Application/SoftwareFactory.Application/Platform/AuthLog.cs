using Microsoft.Extensions.Logging;

namespace SoftwareFactory.Application.Platform;

/// <summary>Structured sign-in events that have no tenant to be audited under (estandar-auth.md §6). Never logs credentials.</summary>
internal static partial class AuthLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Warning, Message = "Sign-in failed: no user matches the given email. IP {IpAddress}.")]
    public static partial void LoginUnknownEmail(this ILogger logger, string? ipAddress);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Sign-in rejected: the email exists in {TenantCount} tenants and the request named none. IP {IpAddress}.")]
    public static partial void LoginAmbiguousEmail(this ILogger logger, int tenantCount, string? ipAddress);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "Sign-in rejected: tenant {TenantId} is inactive or missing. IP {IpAddress}.")]
    public static partial void LoginInactiveTenant(this ILogger logger, Guid tenantId, string? ipAddress);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "Refresh rejected: token unknown, malformed or expired. IP {IpAddress}.")]
    public static partial void RefreshRejected(this ILogger logger, string? ipAddress);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "Refresh token reuse detected for user {UserId}; family {FamilyId} revoked. IP {IpAddress}.")]
    public static partial void RefreshReuseDetected(this ILogger logger, Guid userId, Guid familyId, string? ipAddress);
}
