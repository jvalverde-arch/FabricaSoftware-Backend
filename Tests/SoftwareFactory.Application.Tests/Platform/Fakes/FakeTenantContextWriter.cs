using SoftwareFactory.Application.Common.Tenancy;

namespace SoftwareFactory.Application.Tests.Platform.Fakes;

internal sealed class FakeTenantContextWriter : ITenantContextWriter
{
    public Guid? TenantId { get; private set; }

    public void Establish(Guid tenantId) => TenantId = tenantId;
}
