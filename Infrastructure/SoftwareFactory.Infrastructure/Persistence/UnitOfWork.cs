using SoftwareFactory.Application.Common.Persistence;

namespace SoftwareFactory.Infrastructure.Persistence;

/// <summary>The DbContext is the unit of work: one SaveChanges, one transaction, per service operation.</summary>
public sealed class UnitOfWork(SoftwareFactoryDbContext context) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
