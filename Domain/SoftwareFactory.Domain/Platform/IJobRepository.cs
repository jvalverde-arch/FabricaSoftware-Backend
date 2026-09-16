namespace SoftwareFactory.Domain.Platform;

/// <summary>Queue of agent runs (doc 03, D6). Claiming crosses tenants; everything else is tenant-scoped.</summary>
public interface IJobRepository
{
    void Add(Job job);

    /// <summary>Job of the current tenant, tracked so the run can advance its state.</summary>
    Task<Job?> GetAsync(Guid id, CancellationToken cancellationToken);
}
