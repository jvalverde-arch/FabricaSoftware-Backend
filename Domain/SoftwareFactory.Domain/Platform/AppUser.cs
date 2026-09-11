using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Platform;

/// <summary>
/// Application user of a tenant (estandar-auth.md: application users, not Entra ID). Holds everything the
/// custom Identity store of T-004 needs: Argon2id password hash, lockout counters and the TOTP fields planned for R2.
/// </summary>
public sealed class AppUser : TenantScopedEntity
{
    public const int LockoutThreshold = 5;

    public static readonly TimeSpan BaseLockout = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan MaxLockout = TimeSpan.FromDays(1);

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

    /// <summary>
    /// Registers a failed sign-in. Every <see cref="LockoutThreshold"/> consecutive failures lock the account, starting at
    /// <see cref="BaseLockout"/> and doubling with each further block, capped at <see cref="MaxLockout"/> (estandar-auth.md §2).
    /// Returns the new lockout end when this failure triggered a lock; otherwise null.
    /// </summary>
    public DateTimeOffset? RecordFailedAccess(DateTimeOffset now)
    {
        FailedAccessCount++;
        Touch();

        if (FailedAccessCount % LockoutThreshold != 0)
        {
            return null;
        }

        var blocks = FailedAccessCount / LockoutThreshold;
        var exponent = Math.Min(blocks - 1, 30);
        var duration = BaseLockout * Math.Pow(2, exponent);
        LockoutEnd = now.ToUniversalTime().Add(duration > MaxLockout ? MaxLockout : duration);
        return LockoutEnd;
    }

    public bool IsLockedOut(DateTimeOffset now) => LockoutEnd is { } end && end > now;

    public void ResetFailedAccess()
    {
        FailedAccessCount = 0;
        LockoutEnd = null;
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
