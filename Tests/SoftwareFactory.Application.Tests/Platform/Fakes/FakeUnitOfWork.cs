using SoftwareFactory.Application.Common.Persistence;

namespace SoftwareFactory.Application.Tests.Platform.Fakes;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Commits { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Commits++;
        return Task.CompletedTask;
    }
}
