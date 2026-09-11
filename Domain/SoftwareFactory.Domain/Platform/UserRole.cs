using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Platform;

/// <summary>Assignment of one <see cref="Role"/> to a user; a user may hold several roles.</summary>
public sealed class UserRole : TenantScopedEntity
{
    private UserRole()
    {
    }

    public UserRole(Guid tenantId, Guid userId, Role role)
        : base(tenantId)
    {
        Guard.NotEmpty(userId);
        UserId = userId;
        Role = role;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; private set; }

    public Role Role { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
