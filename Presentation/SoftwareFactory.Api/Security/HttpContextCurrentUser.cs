using System.Security.Claims;
using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Api.Security;

/// <summary>Caller identity read from the validated bearer token of the current request.</summary>
internal sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId => RequiredGuid(AuthClaims.Subject);

    public Guid TenantId => RequiredGuid(AuthClaims.TenantId);

    public IReadOnlyCollection<Role> Roles =>
        Principal is null
            ? []
            : [.. Principal.FindAll(AuthClaims.Roles).Select(claim => RoleNames.TryParse(claim.Value, out var role) ? role : (Role?)null).OfType<Role>()];

    private Guid RequiredGuid(string claimType) =>
        IsAuthenticated && Guid.TryParse(Principal!.FindFirst(claimType)?.Value, out var value) && value != Guid.Empty
            ? value
            : throw new InvalidOperationException($"The request is anonymous or its token lacks the '{claimType}' claim.");
}
