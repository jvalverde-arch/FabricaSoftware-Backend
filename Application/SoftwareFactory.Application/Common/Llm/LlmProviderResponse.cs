namespace SoftwareFactory.Application.Common.Llm;

/// <summary>The text the model produced plus what it cost in tokens. <paramref name="Model"/> is what the provider actually served.</summary>
public sealed record LlmProviderResponse(string Text, string Model, LlmUsage Usage, string? StopReason);
