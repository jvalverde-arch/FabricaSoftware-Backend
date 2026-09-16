using Microsoft.EntityFrameworkCore;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Infrastructure.Persistence.Repositories;

public sealed class JobRepository(SoftwareFactoryDbContext context) : IJobRepository
{
    public void Add(Job job) => context.Jobs.Add(job);

    public Task<Job?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Jobs.SingleOrDefaultAsync(job => job.Id == id, cancellationToken);
}
