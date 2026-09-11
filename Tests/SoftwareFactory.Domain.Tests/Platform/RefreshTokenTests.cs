using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Domain.Tests.Platform;

/// <summary>Refresh tokens of estandar-auth.md §1: opaque, hashed at rest, rotated on every use, sliding expiry, family revocation.</summary>
public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan _lifetime = TimeSpan.FromDays(14);
    private static readonly Guid _tenantId = Guid.CreateVersion7();
    private static readonly Guid _userId = Guid.CreateVersion7();

    [Fact]
    public void Generated_secret_is_256_bits_of_url_safe_text_and_only_its_hash_is_kept()
    {
        var secret = RefreshTokenSecret.Generate();

        Assert.Equal(43, secret.Value.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", secret.Value);
        Assert.NotEqual(secret.Value, secret.Hash);
        Assert.Equal(secret.Hash, RefreshTokenSecret.ComputeHash(secret.Value));
        Assert.NotEqual(secret.Hash, RefreshTokenSecret.Generate().Hash);
    }

    [Fact]
    public void New_token_starts_a_family_and_is_active_until_it_expires()
    {
        var token = RefreshToken.StartFamily(_tenantId, _userId, "hash-1", _now, _lifetime);

        Assert.Equal(token.Id, token.FamilyId);
        Assert.Equal(_now.Add(_lifetime), token.ExpiresAt);
        Assert.True(token.IsActive(_now));
        Assert.True(token.IsActive(_now.Add(_lifetime).AddSeconds(-1)));
        Assert.False(token.IsActive(_now.Add(_lifetime)));
    }

    [Fact]
    public void Rotation_consumes_the_current_token_and_issues_a_sibling_with_sliding_expiry()
    {
        var first = RefreshToken.StartFamily(_tenantId, _userId, "hash-1", _now, _lifetime);
        var later = _now.AddDays(3);

        var second = first.Rotate("hash-2", later, _lifetime);

        Assert.False(first.IsActive(later));
        Assert.Equal(later, first.UsedAt);
        Assert.Equal(second.Id, first.ReplacedByTokenId);
        Assert.Equal(first.FamilyId, second.FamilyId);
        Assert.Equal(_userId, second.UserId);
        Assert.Equal(later.Add(_lifetime), second.ExpiresAt);
        Assert.True(second.IsActive(later));
    }

    [Fact]
    public void A_consumed_or_revoked_token_cannot_be_rotated_again()
    {
        var token = RefreshToken.StartFamily(_tenantId, _userId, "hash-1", _now, _lifetime);
        token.Rotate("hash-2", _now, _lifetime);

        Assert.Throws<InvalidOperationException>(() => token.Rotate("hash-3", _now, _lifetime));

        var revoked = RefreshToken.StartFamily(_tenantId, _userId, "hash-4", _now, _lifetime);
        revoked.Revoke(_now);

        Assert.False(revoked.IsActive(_now));
        Assert.Equal(_now, revoked.RevokedAt);
        Assert.Throws<InvalidOperationException>(() => revoked.Rotate("hash-5", _now, _lifetime));
    }

    [Fact]
    public void Revoking_twice_keeps_the_first_revocation_instant()
    {
        var token = RefreshToken.StartFamily(_tenantId, _userId, "hash-1", _now, _lifetime);
        token.Revoke(_now);

        token.Revoke(_now.AddMinutes(5));

        Assert.Equal(_now, token.RevokedAt);
    }
}
