namespace SoftwareFactory.Application.Common.Llm;

/// <summary>Tokens reported by the provider. Cache counters are zero where the provider does not report them.</summary>
public readonly record struct LlmUsage(int InputTokens, int OutputTokens, int CacheReadTokens, int CacheWriteTokens)
{
    public int TotalTokens => InputTokens + OutputTokens + CacheReadTokens + CacheWriteTokens;
}
