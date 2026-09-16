namespace SoftwareFactory.Application.Common.Jobs;

/// <summary>
/// Run in progress in this scope, or null outside a worker. Everything a job does inherits its id: the LLM gateway
/// stamps it on every <c>llm_call</c>, so the cost of a run can be summed (doc 01, E11).
/// </summary>
public interface ICurrentJob
{
    Guid? JobId { get; }
}
