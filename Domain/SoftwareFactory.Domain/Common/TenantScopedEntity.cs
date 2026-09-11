namespace SoftwareFactory.Domain.Common;

/// <summary>Entity that belongs to exactly one tenant; row-level security filters it by <see cref="TenantId"/>.</summary>
public abstract class TenantScopedEntity : Entity
{
    protected TenantScopedEntity()
    {
    }

    protected TenantScopedEntity(Guid tenantId)
    {
        Guard.NotEmpty(tenantId);
        TenantId = tenantId;
    }

    public Guid TenantId { get; private set; }
}
