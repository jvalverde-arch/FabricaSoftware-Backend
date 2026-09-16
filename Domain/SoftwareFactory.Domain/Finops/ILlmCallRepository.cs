namespace SoftwareFactory.Domain.Finops;

/// <summary>Trace of LLM spending. Append-only in practice: a call that happened is never rewritten.</summary>
public interface ILlmCallRepository
{
    void Add(LlmCall llmCall);

    /// <summary>Sum of what one agent run has spent so far, for the hard budget per job.</summary>
    Task<decimal> GetJobCostAsync(Guid jobId, CancellationToken cancellationToken);
}
