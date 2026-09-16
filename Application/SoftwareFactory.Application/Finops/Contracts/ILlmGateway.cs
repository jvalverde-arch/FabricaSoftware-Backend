namespace SoftwareFactory.Application.Finops.Contracts;

/// <summary>
/// The only door to the models (doc 03, D3). Resolves task → tier → model, measures and records every call in
/// <c>llm_call</c>, and enforces the hard budgets per call and per job.
/// </summary>
public interface ILlmGateway
{
    /// <exception cref="LlmBudgetExceededException">The call or its job reached the configured budget.</exception>
    Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken);
}
