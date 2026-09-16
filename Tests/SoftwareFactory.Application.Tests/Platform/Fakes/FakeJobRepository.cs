using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Tests.Platform.Fakes;

internal sealed class FakeJobRepository : IJobRepository
{
    public List<Job> Items { get; } = [];

    public void Add(Job job) => Items.Add(job);

    public Task<Job?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(job => job.Id == id));
}
