using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Infrastructure.Persistence;
using SoftwareFactory.Infrastructure.Persistence.Security;

namespace SoftwareFactory.Infrastructure.Jobs;

/// <summary>
/// Takes the next runnable job (doc 03, D6). One short transaction does three things: the SECURITY DEFINER function
/// locks the row with <c>FOR UPDATE SKIP LOCKED</c> across tenants, the session then declares that tenant so
/// row-level security lets the entity be read, and the entity moves to running with a fresh lease. The row stays
/// locked until the commit, so two workers never claim the same job.
/// </summary>
internal sealed class JobClaimer(
    SoftwareFactoryDbContext context,
    IOptions<JobWorkerOptions> options,
    TimeProvider clock,
    ILogger<JobClaimer> logger) : IJobClaimer
{
    /// <summary>The function name is a constant of this assembly, never user input; the instant travels as a parameter.</summary>
    private const string ClaimSql =
        $"SELECT job_id AS \"JobId\", job_tenant_id AS \"TenantId\" FROM {RowLevelSecurity.ClaimNextJobFunction}(@now)";

    public async Task<ClaimedJob?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var settings = options.Value;

        var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (transaction.ConfigureAwait(false))
        {
            var candidate = await context.Database
                .SqlQueryRaw<ClaimCandidate>(ClaimSql, new NpgsqlParameter("now", now))
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (candidate is null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }

            // From here on the session is inside the tenant of the job, so every read and write respects RLS.
            var tenant = candidate.TenantId.ToString("D");
            await context.Database
                .ExecuteSqlAsync($"SELECT set_config({RowLevelSecurity.TenantSetting}, {tenant}, true)", cancellationToken)
                .ConfigureAwait(false);

            var job = await context.Jobs.SingleAsync(entity => entity.Id == candidate.JobId, cancellationToken).ConfigureAwait(false);

            if (job.State == Domain.Platform.JobState.Running)
            {
                // Its worker died and the lease expired; the attempt it spent still counts.
                job.ReleaseExpiredLease(now);
            }

            job.Start(now, settings.Lease);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            logger.Claimed(job.Id, job.Attempts, job.State);
            return new ClaimedJob(job.Id, job.TenantId, job.Type, job.Payload, job.Attempts);
        }
    }

    private sealed record ClaimCandidate(Guid JobId, Guid TenantId);
}
