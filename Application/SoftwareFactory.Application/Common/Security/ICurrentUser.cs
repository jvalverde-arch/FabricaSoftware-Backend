using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Application.Common.Security;

/// <summary>Identity of the caller of the current request, taken from the validated access token.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Throws when the caller is anonymous.</summary>
    Guid UserId { get; }

    Guid TenantId { get; }

    IReadOnlyCollection<Role> Roles { get; }
}
