using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Common.Jobs;
using SoftwareFactory.Application.Common.Llm;
using SoftwareFactory.Application.Common.Persistence;
using SoftwareFactory.Application.Common.Tenancy;
using SoftwareFactory.Application.Common.Time;
using SoftwareFactory.Application.Finops.Contracts;
using SoftwareFactory.Domain.Finops;

namespace SoftwareFactory.Application.Finops;

/// <summary>
/// Single door to the models (doc 03, D3). Resolves task → tier → model from configuration, measures latency, records
/// every call in <c>llm_call</c> and enforces the hard budgets of T-006.
/// </summary>
public sealed class LlmGateway(
    IEnumerable<ILlmProvider> providers,
    ILlmCallRepository calls,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    ICurrentJob currentJob,
    IOptions<LlmOptions> options,
    IMonotonicClock clock,
    ILogger<LlmGateway> logger) : ILlmGateway
{
    public async Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("An LLM call needs a tenant in context: its cost belongs to somebody.");

        // A call made inside an agent run belongs to that run even if the caller did not say so: the cost of a run
        // is summed by job_id (doc 01, E11).
        request = request.JobId is null && currentJob.JobId is { } runningJob ? request with { JobId = runningJob } : request;

        var settings = options.Value;
        var resolution = settings.Resolve(request.Task);
        var provider = providers.FirstOrDefault(candidate => string.Equals(candidate.Name, resolution.Provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No LLM provider named '{resolution.Provider}' is registered (tier {resolution.Tier}).");

        var maxOutputTokens = request.MaxOutputTokens ?? resolution.MaxOutputTokens;
        var spentByJob = request.JobId is { } job ? await calls.GetJobCostAsync(job, cancellationToken).ConfigureAwait(false) : 0m;

        GuardBeforeCalling(request, resolution, maxOutputTokens, spentByJob, settings.Budget);

        var providerRequest = new LlmProviderRequest
        {
            Model = resolution.Model,
            Messages = request.Messages,
            System = request.System,
            MaxOutputTokens = maxOutputTokens,
            Temperature = request.Temperature,
        };

        var startedAt = clock.GetTimestamp();
        LlmProviderResponse response;

        try
        {
            response = await provider.CompleteAsync(providerRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // A failed call costs nothing that the provider reports, so nothing is recorded; the failure is logged.
            logger.ProviderFailed(exception, provider.Name, resolution.Model, LatencyOf(startedAt));
            throw;
        }

        var latencyMs = LatencyOf(startedAt);
        var cost = LlmCostCalculator.Compute(response.Usage, resolution.Pricing);

        var call = new LlmCall(
            tenantId,
            request.JobId,
            provider.Name,
            response.Model,
            response.Usage.InputTokens,
            response.Usage.OutputTokens,
            response.Usage.CacheReadTokens,
            response.Usage.CacheWriteTokens,
            latencyMs,
            cost);

        calls.Add(call);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.CallCompleted(
            request.Task,
            resolution.Tier,
            provider.Name,
            response.Model,
            response.Usage.InputTokens,
            response.Usage.OutputTokens,
            response.Usage.CacheReadTokens,
            response.Usage.CacheWriteTokens,
            latencyMs,
            cost);

        // The money is already spent, so the call is recorded first and the budget reported afterwards.
        GuardSpentBudget(request.JobId, cost, spentByJob, settings.Budget);

        return new LlmCompletionResult(
            response.Text,
            resolution.Tier,
            provider.Name,
            response.Model,
            response.Usage,
            cost,
            latencyMs,
            call.Id);
    }

    private int LatencyOf(long startedAt) => (int)Math.Min(clock.GetElapsed(startedAt).TotalMilliseconds, int.MaxValue);

    /// <summary>
    /// Pre-flight of the hard budgets: the worst case of this call (all of its output ceiling) must fit both the
    /// per-call limit and what is left of the job's budget. Refusing here means nothing is spent.
    /// </summary>
    private void GuardBeforeCalling(
        LlmCompletionRequest request,
        LlmResolution resolution,
        int maxOutputTokens,
        decimal spentByJob,
        LlmBudgetOptions budget)
    {
        var estimate = LlmCostCalculator.EstimateWorstCase(
            LlmTokenEstimator.Estimate(request.System, request.Messages),
            maxOutputTokens,
            resolution.Pricing);

        if (estimate > budget.MaxCostPerCall)
        {
            logger.CallRefusedByBudget(estimate, budget.MaxCostPerCall, request.Task, resolution.Model);
            throw new LlmBudgetExceededException(LlmBudgetScope.Call, budget.MaxCostPerCall, estimate, request.JobId);
        }

        if (request.JobId is { } job && spentByJob + estimate > budget.MaxCostPerJob)
        {
            logger.BudgetExceeded(LlmBudgetScope.Job, budget.MaxCostPerJob, spentByJob, job);
            throw new LlmBudgetExceededException(LlmBudgetScope.Job, budget.MaxCostPerJob, spentByJob, job);
        }
    }

    private void GuardSpentBudget(Guid? jobId, decimal cost, decimal spentByJob, LlmBudgetOptions budget)
    {
        if (cost > budget.MaxCostPerCall)
        {
            logger.BudgetExceeded(LlmBudgetScope.Call, budget.MaxCostPerCall, cost, jobId);
            throw new LlmBudgetExceededException(LlmBudgetScope.Call, budget.MaxCostPerCall, cost, jobId);
        }

        if (jobId is { } job && spentByJob + cost > budget.MaxCostPerJob)
        {
            logger.BudgetExceeded(LlmBudgetScope.Job, budget.MaxCostPerJob, spentByJob + cost, job);
            throw new LlmBudgetExceededException(LlmBudgetScope.Job, budget.MaxCostPerJob, spentByJob + cost, job);
        }
    }
}
