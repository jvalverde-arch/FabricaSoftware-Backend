using Microsoft.Extensions.Logging;
using SoftwareFactory.Application.Common.Persistence;
using SoftwareFactory.Application.Common.Tenancy;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Infrastructure.Jobs;

/// <summary>Queues a run for the tenant of the current request (doc 03, D6: the queue is a table, not a broker).</summary>
internal sealed class JobEnqueuer(
    IJobRepository jobs,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    ILogger<JobEnqueuer> logger) : IJobEnqueuer
{
    public async Task<Guid> EnqueueAsync(string type, string payload, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("A job needs a tenant in context: the run belongs to somebody.");

        var job = new Job(tenantId, type, payload);
        jobs.Add(job);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.Queued(job.Id, job.Type, tenantId);
        return job.Id;
    }
}
