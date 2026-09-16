using SoftwareFactory.Application.Common.Llm;

namespace SoftwareFactory.Application.Finops.Contracts;

/// <summary>The answer plus its measured cost: what E11 needs to show consumption per operation.</summary>
public sealed record LlmCompletionResult(
    string Text,
    LlmTier Tier,
    string Provider,
    string Model,
    LlmUsage Usage,
    decimal Cost,
    int LatencyMs,
    Guid LlmCallId);
