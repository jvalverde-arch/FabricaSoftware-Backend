using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Application.Tests.Platform.Fakes;

internal sealed class FakeCurrentUser : ICurrentUser
{
    private Guid? _userId;
    private Guid? _tenantId;

    public bool IsAuthenticated => _userId is not null;

    public Guid UserId => _userId ?? throw new InvalidOperationException("Anonymous.");

    public Guid TenantId => _tenantId ?? throw new InvalidOperationException("Anonymous.");

    public IReadOnlyCollection<Role> Roles { get; private set; } = [];

    public void SignIn(Guid userId, Guid tenantId, params Role[] roles)
    {
        _userId = userId;
        _tenantId = tenantId;
        Roles = roles;
    }
}
