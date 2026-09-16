using Microsoft.Extensions.Logging;

namespace SoftwareFactory.Application.Finops;

/// <summary>
/// Structured events of the LLM gateway (estandar-backend.md §3). Prompts and answers are never logged: only the
/// task, the model and what it cost.
/// </summary>
internal static partial class LlmLog
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information,
        Message = "LLM call: task {Task}, tier {Tier}, provider {Provider}, model {Model}, {InputTokens} in / {OutputTokens} out (cache {CacheReadTokens} read / {CacheWriteTokens} write), {LatencyMs} ms, cost {Cost}.")]
    public static partial void CallCompleted(
        this ILogger logger,
        string task,
        Contracts.LlmTier tier,
        string provider,
        string model,
        int inputTokens,
        int outputTokens,
        int cacheReadTokens,
        int cacheWriteTokens,
        int latencyMs,
        decimal cost);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning,
        Message = "LLM call refused before dispatch: estimated cost {Estimate} exceeds the per-call budget {Limit} (task {Task}, model {Model}).")]
    public static partial void CallRefusedByBudget(this ILogger logger, decimal estimate, decimal limit, string task, string model);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Warning,
        Message = "LLM call exceeded its budget: {Scope} limit {Limit}, spent {Spent} (job {JobId}).")]
    public static partial void BudgetExceeded(this ILogger logger, Contracts.LlmBudgetScope scope, decimal limit, decimal spent, Guid? jobId);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Error,
        Message = "LLM provider {Provider} failed for model {Model} after {LatencyMs} ms.")]
    public static partial void ProviderFailed(this ILogger logger, Exception exception, string provider, string model, int latencyMs);
}
