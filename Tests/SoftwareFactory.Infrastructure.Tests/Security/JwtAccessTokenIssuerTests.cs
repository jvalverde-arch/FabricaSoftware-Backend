using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Security.Jwt;

namespace SoftwareFactory.Infrastructure.Tests.Security;

/// <summary>Access token of estandar-auth.md §1: 15 minutes, own iss/aud, sub + tenant_id + roles + name, kid header, two-key window.</summary>
public sealed class JwtAccessTokenIssuerTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    private static readonly JwtOptions _options = new()
    {
        Issuer = "https://softwarefactory.test",
        Audience = "softwarefactory-api",
        ActiveKeyId = "2026-09",
        SigningKeys =
        [
            new JwtSigningKey { KeyId = "2026-06", Secret = "retired-key-0123456789-0123456789-0123456789" },
            new JwtSigningKey { KeyId = "2026-09", Secret = "active-key-0123456789-0123456789-0123456789!" },
        ],
    };

    [Fact]
    public async Task Issues_a_token_with_the_expected_claims_lifetime_and_active_key()
    {
        var clock = new FakeClock(_now);
        var user = new AppUser(Guid.CreateVersion7(), "user@tenant.test", "Test user", "$argon2id$v=19$m=8,t=1,p=1$c2FsdA$aGFzaA");
        var issuer = new JwtAccessTokenIssuer(Options.Create(_options), clock);

        var token = issuer.Issue(user, [Role.Functional, Role.Qa]);

        Assert.Equal(_now.AddMinutes(15), token.ExpiresAt);

        var parsed = new JsonWebToken(token.Value);
        Assert.Equal("2026-09", parsed.Kid);
        Assert.Equal(user.Id.ToString("D"), parsed.Subject);
        Assert.Equal(user.TenantId.ToString("D"), parsed.GetClaim(AuthClaims.TenantId).Value);
        Assert.Equal("Test user", parsed.GetClaim(AuthClaims.Name).Value);
        Assert.Equal(["functional", "qa"], parsed.Claims.Where(claim => claim.Type == AuthClaims.Roles).Select(claim => claim.Value).Order());

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, JwtTokenValidation.CreateParameters(_options, clock));
        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Fact]
    public async Task Tokens_signed_with_a_listed_retired_key_still_validate_and_unknown_keys_do_not()
    {
        var clock = new FakeClock(_now);
        var user = new AppUser(Guid.CreateVersion7(), "user@tenant.test", "Test user", "$argon2id$v=19$m=8,t=1,p=1$c2FsdA$aGFzaA");
        var retired = new JwtOptions
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            ActiveKeyId = "2026-06",
            SigningKeys = [_options.SigningKeys[0]],
        };
        var unknown = new JwtOptions
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            ActiveKeyId = "rogue",
            SigningKeys = [new JwtSigningKey { KeyId = "rogue", Secret = "rogue-key-0123456789-0123456789-0123456789!!" }],
        };

        var withRetired = new JwtAccessTokenIssuer(Options.Create(retired), clock).Issue(user, [Role.Reader]);
        var withUnknown = new JwtAccessTokenIssuer(Options.Create(unknown), clock).Issue(user, [Role.Reader]);
        var parameters = JwtTokenValidation.CreateParameters(_options, clock);
        var handler = new JsonWebTokenHandler();

        Assert.True((await handler.ValidateTokenAsync(withRetired.Value, parameters)).IsValid);
        Assert.False((await handler.ValidateTokenAsync(withUnknown.Value, parameters)).IsValid);
    }

    [Fact]
    public async Task Expired_tokens_are_rejected_without_clock_skew()
    {
        var clock = new FakeClock(_now);
        var user = new AppUser(Guid.CreateVersion7(), "user@tenant.test", "Test user", "$argon2id$v=19$m=8,t=1,p=1$c2FsdA$aGFzaA");
        var token = new JwtAccessTokenIssuer(Options.Create(_options), clock).Issue(user, [Role.Reader]);

        clock.Advance(TimeSpan.FromMinutes(15).Add(TimeSpan.FromSeconds(1)));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, JwtTokenValidation.CreateParameters(_options, clock));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Options_validation_requires_issuer_audience_and_a_listed_active_key_of_at_least_32_bytes()
    {
        Assert.True(_options.IsValid());
        Assert.False(new JwtOptions { Issuer = "x", Audience = "y", ActiveKeyId = "k", SigningKeys = [new JwtSigningKey { KeyId = "k", Secret = "too-short" }] }.IsValid());
        Assert.False(new JwtOptions { Issuer = "x", Audience = "y", ActiveKeyId = "missing", SigningKeys = _options.SigningKeys }.IsValid());
        Assert.False(new JwtOptions { Issuer = "", Audience = "y", ActiveKeyId = "2026-09", SigningKeys = _options.SigningKeys }.IsValid());
    }
}
