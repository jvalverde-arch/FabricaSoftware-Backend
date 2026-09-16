using Microsoft.EntityFrameworkCore;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Infrastructure.Persistence;

namespace SoftwareFactory.Infrastructure.Jobs;

/// <summary>Reads runs of the current tenant. Row-level security keeps one tenant from watching another's progress.</summary>
internal sealed class JobReader(SoftwareFactoryDbContext context) : IJobReader
{
    public async Task<JobSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await context.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(job => job.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return job is null ? null : JobSnapshot.Of(job);
    }
}
