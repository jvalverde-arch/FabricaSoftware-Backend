using Microsoft.EntityFrameworkCore;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Infrastructure.Persistence.Repositories;

public sealed class TenantRepository(SoftwareFactoryDbContext context) : ITenantRepository
{
    public Task<Tenant?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Tenants.AsNoTracking().SingleOrDefaultAsync(tenant => tenant.Id == id, cancellationToken);
}
