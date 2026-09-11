using System.Text;
using Microsoft.IdentityModel.Tokens;
using SoftwareFactory.Application.Common.Security;

namespace SoftwareFactory.Infrastructure.Security.Jwt;

/// <summary>Validation parameters shared by the Api's bearer scheme and the tests: every listed key is accepted, no clock skew.</summary>
public static class JwtTokenValidation
{
    public static TokenValidationParameters CreateParameters(JwtOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        return new TokenValidationParameters
        {
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            IssuerSigningKeys = options.SigningKeys.Select(ToSecurityKey).ToList(),
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = AuthClaims.Name,
            RoleClaimType = AuthClaims.Roles,
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = clock.GetUtcNow().UtcDateTime;
                return expires is { } exp && exp > now && (notBefore is not { } nbf || nbf <= now);
            },
        };
    }

    internal static SymmetricSecurityKey ToSecurityKey(JwtSigningKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key.Secret)) { KeyId = key.KeyId };
    }
}
