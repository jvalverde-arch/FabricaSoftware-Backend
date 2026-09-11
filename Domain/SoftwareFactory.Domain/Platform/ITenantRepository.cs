namespace SoftwareFactory.Domain.Platform;

public interface ITenantRepository
{
    Task<Tenant?> GetAsync(Guid id, CancellationToken cancellationToken);
}
