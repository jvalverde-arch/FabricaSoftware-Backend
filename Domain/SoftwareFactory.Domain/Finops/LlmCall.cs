using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Finops;

/// <summary>Trace of one LLM call: provider, model, tokens, latency and computed cost (doc 01, E11; T-006 writes it).</summary>
public sealed class LlmCall : TenantScopedEntity
{
    private LlmCall()
    {
    }

    public LlmCall(
        Guid tenantId,
        Guid? jobId,
        string provider,
        string model,
        int inputTokens,
        int outputTokens,
        int cacheReadTokens,
        int cacheWriteTokens,
        int latencyMs,
        decimal cost)
        : base(tenantId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(outputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(cacheReadTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(cacheWriteTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(latencyMs);
        ArgumentOutOfRangeException.ThrowIfNegative(cost);
        JobId = jobId;
        Provider = Guard.NotBlank(provider);
        Model = Guard.NotBlank(model);
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CacheReadTokens = cacheReadTokens;
        CacheWriteTokens = cacheWriteTokens;
        LatencyMs = latencyMs;
        Cost = cost;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid? JobId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public int InputTokens { get; private set; }

    public int OutputTokens { get; private set; }

    public int CacheReadTokens { get; private set; }

    public int CacheWriteTokens { get; private set; }

    public int LatencyMs { get; private set; }

    /// <summary>Cost in the platform currency, numeric(12,6).</summary>
    public decimal Cost { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
