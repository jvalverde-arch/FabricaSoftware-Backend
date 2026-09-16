namespace SoftwareFactory.Application.Common.Llm;

/// <summary>What the adapter sends to a concrete provider once the model has been resolved.</summary>
public sealed record LlmProviderRequest
{
    public required string Model { get; init; }

    public required IReadOnlyList<LlmMessage> Messages { get; init; }

    /// <summary>Optional system prompt; providers that have no system role prepend it as the first message.</summary>
    public string? System { get; init; }

    public required int MaxOutputTokens { get; init; }

    public decimal? Temperature { get; init; }
}
