using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Common.Tenancy;
using SoftwareFactory.Application.Platform;
using SoftwareFactory.Application.Platform.Contracts;

namespace SoftwareFactory.AgentRuntime.Jobs;

/// <summary>
/// Consumes the job queue (doc 03, D6). Each round claims in its own scope — the claim runs without a tenant — and
/// then executes in a fresh scope with the tenant of the job established, so every query of the run is filtered by
/// row-level security and every LLM call it makes carries the job id.
/// </summary>
public sealed class JobWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<JobWorkerOptions> options,
    ILogger<JobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.Enabled)
        {
            logger.WorkerDisabled();
            return;
        }

        logger.WorkerStarted(settings.PollInterval, settings.Lease);

        while (!stoppingToken.IsCancellationRequested)
        {
            var worked = false;

            try
            {
                worked = await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // The loop must survive any failure of a round: a dead worker drains no queue.
            catch (Exception exception)
            {
                logger.RoundFailed(exception);
            }
#pragma warning restore CA1031

            if (!worked)
            {
                await SleepAsync(settings.PollInterval, stoppingToken).ConfigureAwait(false);
            }
        }

        logger.WorkerStopped();
    }

    private async Task<bool> RunOnceAsync(CancellationToken cancellationToken)
    {
        ClaimedJob? claimed;

        await using (var claimScope = scopeFactory.CreateAsyncScope())
        {
            claimed = await claimScope.ServiceProvider
                .GetRequiredService<IJobClaimer>()
                .ClaimNextAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (claimed is null)
        {
            return false;
        }

        await using var runScope = scopeFactory.CreateAsyncScope();
        runScope.ServiceProvider.GetRequiredService<ITenantContextWriter>().Establish(claimed.TenantId);
        await runScope.ServiceProvider
            .GetRequiredService<JobDispatcher>()
            .ExecuteAsync(claimed, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    private static async Task SleepAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutting down: nothing to report.
        }
    }
}
