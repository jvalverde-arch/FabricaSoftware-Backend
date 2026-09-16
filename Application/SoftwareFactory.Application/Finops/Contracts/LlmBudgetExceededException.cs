using System.Globalization;

namespace SoftwareFactory.Application.Finops.Contracts;

/// <summary>
/// Controlled failure of T-006 when a hard budget is reached. Thrown before calling the provider when the worst case
/// of the call or what the job already spent exceeds the limit, and after recording the call when the answer did.
/// </summary>
public sealed class LlmBudgetExceededException : Exception
{
    public LlmBudgetExceededException(LlmBudgetScope scope, decimal limit, decimal spent, Guid? jobId = null)
        : base(Describe(scope, limit, spent, jobId))
    {
        Scope = scope;
        Limit = limit;
        Spent = spent;
        JobId = jobId;
    }

    public LlmBudgetExceededException()
        : this(LlmBudgetScope.Call, 0m, 0m)
    {
    }

    public LlmBudgetExceededException(string message)
        : base(message)
    {
    }

    public LlmBudgetExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public LlmBudgetScope Scope { get; }

    public decimal Limit { get; }

    /// <summary>What was already spent (job scope) or what the call ended up costing (call scope).</summary>
    public decimal Spent { get; }

    public Guid? JobId { get; }

    private static string Describe(LlmBudgetScope scope, decimal limit, decimal spent, Guid? jobId) =>
        scope == LlmBudgetScope.Job
            ? string.Create(CultureInfo.InvariantCulture, $"Job {jobId} reached its LLM budget: spent {spent}, limit {limit}.")
            : string.Create(CultureInfo.InvariantCulture, $"The call reached the per-call LLM budget: cost {spent}, limit {limit}.");
}
