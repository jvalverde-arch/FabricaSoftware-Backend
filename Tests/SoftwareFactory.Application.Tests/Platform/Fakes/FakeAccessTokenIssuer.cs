using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Tests.Platform.Fakes;

internal sealed class FakeAccessTokenIssuer(TimeProvider clock) : IAccessTokenIssuer
{
    public AccessToken Issue(AppUser user, IReadOnlyCollection<Role> roles) =>
        new($"access:{user.Id}:{string.Join('|', roles)}", clock.GetUtcNow().AddMinutes(15));
}
