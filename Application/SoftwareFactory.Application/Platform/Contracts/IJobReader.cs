namespace SoftwareFactory.Application.Platform.Contracts;

/// <summary>Reads runs of the current tenant, for the job endpoint and the progress stream.</summary>
public interface IJobReader
{
    Task<JobSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken);
}
