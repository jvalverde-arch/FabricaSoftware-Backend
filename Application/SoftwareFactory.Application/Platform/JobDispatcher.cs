using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Common.Jobs;
using SoftwareFactory.Application.Common.Persistence;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Platform;

/// <summary>
/// Runs one claimed job: resolves its handler, reports progress, and closes the run as success, retry with backoff,
/// or definitive failure (doc 03, D6). The tenant is already established by the caller; this establishes the run, so
/// every LLM call inside it lands in <c>llm_call</c> with its <c>job_id</c> (doc 01, E11).
/// </summary>
public sealed class JobDispatcher(
    IEnumerable<IJobHandler> handlers,
    IJobRepository jobs,
    IUnitOfWork unitOfWork,
    ICurrentJobWriter currentJob,
    IOptions<JobWorkerOptions> options,
    TimeProvider clock,
    ILogger<JobDispatcher> logger)
{
    public async Task ExecuteAsync(ClaimedJob claimed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claimed);

        var job = await jobs.GetAsync(claimed.JobId, cancellationToken).ConfigureAwait(false);

        if (job is null)
        {
            logger.Vanished(claimed.JobId);
            return;
        }

        currentJob.Establish(job.Id);
        var settings = options.Value;
        var handler = handlers.FirstOrDefault(candidate => string.Equals(candidate.JobType, claimed.Type, StringComparison.Ordinal));

        if (handler is null)
        {
            logger.UnknownType(job.Id, claimed.Type);
            await CloseAsync(job, $"No handler serves job type '{claimed.Type}'.", exception: null, final: true, cancellationToken).ConfigureAwait(false);
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var execution = new JobExecution(claimed, (phase, percent, token) => ReportAsync(job, phase, percent, settings, token));
            await handler.HandleAsync(execution, cancellationToken).ConfigureAwait(false);

            job.Succeed(clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            logger.Succeeded(job.Id, job.Type, job.Attempts, elapsed);
        }
        catch (OperationCanceledException)
        {
            // Shutdown, not failure: the lease is released so another worker picks the job up without burning an attempt.
            job.ReleaseExpiredLease(clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            logger.Interrupted(job.Id);
            throw;
        }
#pragma warning disable CA1031 // A handler may throw anything; the queue turns that into a retry instead of killing the worker.
        catch (Exception exception)
        {
            await CloseAsync(job, exception.Message, exception, final: false, cancellationToken).ConfigureAwait(false);
        }
#pragma warning restore CA1031
    }

    private async Task CloseAsync(Job job, string error, Exception? exception, bool final, CancellationToken cancellationToken)
    {
        var policy = final ? new JobRetryPolicy(job.Attempts, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)) : options.Value.RetryPolicy();
        var retrying = job.Fail(error, clock.GetUtcNow(), policy);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (retrying)
        {
            logger.Retrying(exception!, job.Id, job.Type, job.Attempts, job.AvailableAt);
        }
        else
        {
            logger.GaveUp(exception, job.Id, job.Type, job.Attempts);
        }
    }

    private async Task ReportAsync(Job job, string phase, int? percent, JobWorkerOptions settings, CancellationToken cancellationToken)
    {
        job.ReportProgress(phase, percent, clock.GetUtcNow(), settings.Lease);
        // Persisted right away: the progress stream reads the row, so an unsaved phase is a phase nobody sees.
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.Progress(job.Id, phase, percent);
    }
}
