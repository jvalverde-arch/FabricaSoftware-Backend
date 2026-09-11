using SoftwareFactory.Application.Common.Tenancy;
using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Infrastructure.Persistence.Tenancy;

/// <summary>Per-scope tenant holder. The authentication middleware (T-004) sets it once per request from the token.</summary>
public sealed class ScopedTenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public void Set(Guid tenantId)
    {
        Guard.NotEmpty(tenantId);
        TenantId = tenantId;
    }

    public void Clear() => TenantId = null;
}
