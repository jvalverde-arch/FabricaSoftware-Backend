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

    // Traceability (S1): every change to an artifact leaves a trace (HU-001 §6).
    ArtifactCreated,
    ArtifactUpdated,
    ArtifactStateChanged,
    ArtifactDeleted,
    RelationCreated,
    RelationDeleted,
}
