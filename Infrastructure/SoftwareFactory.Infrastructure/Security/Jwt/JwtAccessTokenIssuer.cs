using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Infrastructure.Security.Jwt;

/// <summary>Issues the platform access token: jti, sub, tenant_id, roles, name; signed with the active key.</summary>
internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    public AccessToken Issue(AppUser user, IReadOnlyCollection<Role> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        var settings = options.Value;
        var now = clock.GetUtcNow();
        var expiresAt = now.Add(settings.AccessTokenLifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(JwtTokenValidation.ToSecurityKey(settings.ActiveKey), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [AuthClaims.TokenId] = Guid.CreateVersion7().ToString("N"),
                [AuthClaims.Subject] = user.Id.ToString("D"),
                [AuthClaims.TenantId] = user.TenantId.ToString("D"),
                [AuthClaims.Name] = user.DisplayName,
                [AuthClaims.Roles] = roles.Select(RoleNames.Of).ToArray(),
            },
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
