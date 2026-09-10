using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Platform;

/// <summary>
/// Application user of a tenant (estandar-auth.md: application users, not Entra ID). Holds everything the
/// custom Identity store of T-004 needs: Argon2id password hash, lockout counters and the TOTP fields planned for R2.
/// </summary>
public sealed class AppUser : TenantScopedEntity
{
    private AppUser()
    {
    }

    public AppUser(Guid tenantId, string email, string displayName, string passwordHash)
        : base(tenantId)
    {
        Email = Guard.NotBlank(email);
        NormalizedEmail = Normalize(Email);
        DisplayName = Guard.NotBlank(displayName);
        PasswordHash = Guard.NotBlank(passwordHash);
        SecurityStamp = Guid.NewGuid().ToString("N");
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Email { get; private set; } = string.Empty;

    /// <summary>Upper-invariant email used for the per-tenant uniqueness constraint and lookups.</summary>
    public string NormalizedEmail { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Argon2id hash in PHC string format (<c>$argon2id$v=19$m=...,t=...,p=...$salt$hash</c>).</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>Changes whenever credentials change; lets issued tokens be invalidated.</summary>
    public string SecurityStamp { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public int FailedAccessCount { get; private set; }

    public DateTimeOffset? LockoutEnd { get; private set; }

    public bool TwoFactorEnabled { get; private set; }

    /// <summary>TOTP shared secret; data model planned from R1, feature enabled in R2.</summary>
    public string? TwoFactorSecret { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static string Normalize(string email) => Guard.NotBlank(email).ToUpperInvariant();

    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = Guard.NotBlank(passwordHash);
        SecurityStamp = Guid.NewGuid().ToString("N");
        Touch();
    }

    public void RecordFailedAccess()
    {
        FailedAccessCount++;
        Touch();
    }

    public void ResetFailedAccess()
    {
        FailedAccessCount = 0;
        LockoutEnd = null;
        Touch();
    }

    public void LockUntil(DateTimeOffset lockoutEnd)
    {
        LockoutEnd = lockoutEnd.ToUniversalTime();
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        SecurityStamp = Guid.NewGuid().ToString("N");
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
