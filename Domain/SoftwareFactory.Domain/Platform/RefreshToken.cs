using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Platform;

/// <summary>
/// One link of a refresh-token family (estandar-auth.md §1). Only the SHA-256 hash of the opaque secret is stored. A token is
/// consumed by rotation, which issues its successor in the same family with a sliding expiry; a family is revoked as a whole
/// on logout, password change or when a consumed token is presented again.
/// </summary>
public sealed class RefreshToken : TenantScopedEntity
{
    private RefreshToken()
    {
    }

    private RefreshToken(Guid tenantId, Guid userId, Guid? familyId, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
        : base(tenantId)
    {
        Guard.NotEmpty(userId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        UserId = userId;
        FamilyId = familyId ?? Id;
        TokenHash = Guard.NotBlank(tokenHash);
        CreatedAt = now.ToUniversalTime();
        ExpiresAt = CreatedAt.Add(lifetime);
    }

    public Guid UserId { get; private set; }

    /// <summary>Id of the first token of the session; every rotation keeps it so the whole session can be revoked.</summary>
    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Instant the token was consumed by a rotation; a consumed token presented again means reuse.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public static RefreshToken StartFamily(Guid tenantId, Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) =>
        new(tenantId, userId, null, tokenHash, now, lifetime);

    public bool IsActive(DateTimeOffset now) => UsedAt is null && RevokedAt is null && ExpiresAt > now;

    /// <summary>Consumes this token and returns its successor in the same family.</summary>
    public RefreshToken Rotate(string successorHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (UsedAt is not null || RevokedAt is not null)
        {
            throw new InvalidOperationException("A consumed or revoked refresh token cannot be rotated.");
        }

        var successor = new RefreshToken(TenantId, UserId, FamilyId, successorHash, now, lifetime);
        UsedAt = now.ToUniversalTime();
        ReplacedByTokenId = successor.Id;
        return successor;
    }

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now.ToUniversalTime();
}
