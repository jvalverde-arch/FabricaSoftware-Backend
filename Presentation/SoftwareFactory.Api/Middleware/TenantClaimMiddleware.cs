using Microsoft.AspNetCore.Authentication;
using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Application.Common.Tenancy;

namespace SoftwareFactory.Api.Middleware;

/// <summary>
/// Establishes the tenant of the request from the <c>tenant_id</c> claim of the validated token, and from nowhere else
/// (estandar-auth.md §1). Row-level security then filters every query of the request. A token without a usable claim is refused.
/// </summary>
internal sealed class TenantClaimMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContextWriter tenantContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (!Guid.TryParse(context.User.FindFirst(AuthClaims.TenantId)?.Value, out var tenantId) || tenantId == Guid.Empty)
            {
                await context.ChallengeAsync();
                return;
            }

            tenantContext.Establish(tenantId);
        }

        await next(context);
    }
}
