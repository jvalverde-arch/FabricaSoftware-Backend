using SoftwareFactory.Application.Common.Persistence;

namespace SoftwareFactory.Application.Tests.Platform.Fakes;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Commits { get; private set; }

    /// <summary>Set to make the next commit lose against a unique index, which is what a race looks like from here.</summary>
    public string? RefusedByIndex { get; set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (RefusedByIndex is { } constraint)
        {
            RefusedByIndex = null;
            throw new UniqueConstraintViolationException(constraint, new InvalidOperationException("duplicate key"));
        }

        Commits++;
        return Task.CompletedTask;
    }
}
