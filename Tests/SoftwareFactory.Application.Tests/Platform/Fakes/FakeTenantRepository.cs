using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Tests.Platform.Fakes;

internal sealed class FakeTenantRepository : ITenantRepository
{
    public List<Tenant> Tenants { get; } = [];

    public Task<Tenant?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Tenants.SingleOrDefault(tenant => tenant.Id == id));
}
