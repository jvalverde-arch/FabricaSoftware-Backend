namespace SoftwareFactory.Application.Common.Persistence;

/// <summary>Commits the pending changes of the current operation (estandar-backend.md §2: one transaction per service operation).</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
