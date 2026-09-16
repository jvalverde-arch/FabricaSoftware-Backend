using SoftwareFactory.Application.Common.Llm;

namespace SoftwareFactory.Application.Finops.Contracts;

/// <summary>
/// What a caller asks for: a named task (which decides the tier and therefore the model) and the conversation.
/// <see cref="JobId"/> ties the spending to an agent run so its cost can be summed.
/// </summary>
public sealed record LlmCompletionRequest(string Task, IReadOnlyList<LlmMessage> Messages)
{
    public string? System { get; init; }

    /// <summary>Ceiling for the answer; defaults to the tier's own limit.</summary>
    public int? MaxOutputTokens { get; init; }

    public decimal? Temperature { get; init; }

    public Guid? JobId { get; init; }
}
