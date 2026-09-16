using Microsoft.EntityFrameworkCore;
using SoftwareFactory.Domain.Finops;

namespace SoftwareFactory.Infrastructure.Persistence.Repositories;

public sealed class LlmCallRepository(SoftwareFactoryDbContext context) : ILlmCallRepository
{
    public void Add(LlmCall llmCall) => context.LlmCalls.Add(llmCall);

    /// <summary>
    /// What one agent run has spent. Row-level security keeps the sum inside the tenant of the session, and calls
    /// added in this unit of work but not yet committed are included so a budget cannot be dodged inside a batch.
    /// </summary>
    public async Task<decimal> GetJobCostAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var committed = await context.LlmCalls
            .AsNoTracking()
            .Where(call => call.JobId == jobId)
            .SumAsync(call => (decimal?)call.Cost, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        var pending = context.ChangeTracker
            .Entries<LlmCall>()
            .Where(entry => entry.State == EntityState.Added && entry.Entity.JobId == jobId)
            .Sum(entry => entry.Entity.Cost);

        return committed + pending;
    }
}
