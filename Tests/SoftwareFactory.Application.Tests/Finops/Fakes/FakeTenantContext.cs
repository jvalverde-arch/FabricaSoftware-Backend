using SoftwareFactory.Application.Common.Tenancy;

namespace SoftwareFactory.Application.Tests.Finops.Fakes;

internal sealed class FakeTenantContext(Guid? tenantId) : ITenantContext
{
    public Guid? TenantId { get; } = tenantId;
}
