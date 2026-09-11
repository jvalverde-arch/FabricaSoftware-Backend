using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Common.Security;

/// <summary>Issues the short-lived signed access token of estandar-auth.md §1.</summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(AppUser user, IReadOnlyCollection<Role> roles);
}
