using Microsoft.AspNetCore.Authorization;
using SoftwareFactory.Application.Common.Security;

namespace SoftwareFactory.Api.Security;

/// <summary>
/// One policy per role, named after the role (estandar-auth.md §4), plus a fallback that makes every endpoint require a
/// token unless it opts out with <c>[AllowAnonymous]</c> (login, refresh, logout and /health).
/// </summary>
internal static class AuthorizationPolicies
{
    public static void Configure(AuthorizationOptions options)
    {
        foreach (var role in RoleNames.All)
        {
            options.AddPolicy(role, policy => policy.RequireAuthenticatedUser().RequireClaim(AuthClaims.Roles, role));
        }

        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    }
}
