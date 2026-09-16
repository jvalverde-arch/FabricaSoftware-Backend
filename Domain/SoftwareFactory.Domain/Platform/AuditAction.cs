namespace SoftwareFactory.Domain.Platform;

/// <summary>Audited actions. Authentication events first (estandar-auth.md §6); functional modules extend the list.</summary>
public enum AuditAction
{
    LoginSucceeded,
    LoginFailed,
    Lockout,
    RefreshReuseDetected,
    Logout,
    PasswordChanged,
}
