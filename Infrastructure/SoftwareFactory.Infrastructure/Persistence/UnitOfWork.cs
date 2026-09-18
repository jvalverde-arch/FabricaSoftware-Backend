using Microsoft.EntityFrameworkCore;
using Npgsql;
using SoftwareFactory.Application.Common.Persistence;

namespace SoftwareFactory.Infrastructure.Persistence;

/// <summary>The DbContext is the unit of work: one SaveChanges, one transaction, per service operation.</summary>
public sealed class UnitOfWork(SoftwareFactoryDbContext context) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && string.Equals(postgres.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal))
        {
            // The unique index is the authority (estandar-backend.md §4). Translating it here is what lets a
            // service answer «that name is taken» to the write that lost the race instead of a 500.
            throw new UniqueConstraintViolationException(postgres.ConstraintName ?? string.Empty, exception);
        }
    }
}
