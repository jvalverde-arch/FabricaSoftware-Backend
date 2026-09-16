using SoftwareFactory.Application.Common.Tenancy;
using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Infrastructure.Persistence.Tenancy;

/// <summary>Per-scope tenant holder. The tenant middleware sets it from the token claim; sign-in sets it from the user it finds.</summary>
public sealed class ScopedTenantContext : ITenantContext, ITenantContextWriter
{
    public Guid? TenantId { get; private set; }

    public void Establish(Guid tenantId)
    {
        Guard.NotEmpty(tenantId);
        TenantId = tenantId;
    }
}
