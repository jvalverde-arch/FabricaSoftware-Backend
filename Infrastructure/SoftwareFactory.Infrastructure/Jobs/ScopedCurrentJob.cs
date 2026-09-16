using SoftwareFactory.Application.Common.Jobs;
using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Infrastructure.Jobs;

/// <summary>Run of the current scope. The worker establishes it once per job; outside a worker it stays empty.</summary>
public sealed class ScopedCurrentJob : ICurrentJob, ICurrentJobWriter
{
    public Guid? JobId { get; private set; }

    public void Establish(Guid jobId)
    {
        Guard.NotEmpty(jobId);
        JobId = jobId;
    }
}
