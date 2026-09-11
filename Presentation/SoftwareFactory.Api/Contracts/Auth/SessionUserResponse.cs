using SoftwareFactory.Application.Platform.Contracts;

namespace SoftwareFactory.Api.Contracts.Auth;

public sealed record SessionUserResponse(Guid Id, Guid TenantId, string Email, string DisplayName, IReadOnlyCollection<string> Roles)
{
    public static SessionUserResponse From(SessionUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new SessionUserResponse(user.Id, user.TenantId, user.Email, user.DisplayName, user.Roles);
    }
}
