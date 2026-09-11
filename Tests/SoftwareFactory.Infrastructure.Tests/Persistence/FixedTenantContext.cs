using SoftwareFactory.Application.Common.Tenancy;

namespace SoftwareFactory.Infrastructure.Tests.Persistence;

internal sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
{
    public Guid? TenantId { get; } = tenantId;
}
