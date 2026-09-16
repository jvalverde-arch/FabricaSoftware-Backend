namespace SoftwareFactory.Application.Finops.Contracts;

/// <summary>
/// Price of a model in platform currency per million tokens (doc 04 §1). A self-hosted model leaves everything at zero,
/// which makes its calls free but still measured.
/// </summary>
public sealed class LlmModelPricing
{
    public decimal InputPerMillion { get; set; }

    public decimal OutputPerMillion { get; set; }

    /// <summary>Anthropic reads cache at 0.1x the input price (doc 04 §1).</summary>
    public decimal CacheReadPerMillion { get; set; }

    /// <summary>Writing to the cache costs more than plain input; 1.25x with Anthropic.</summary>
    public decimal CacheWritePerMillion { get; set; }

    public bool IsValid() =>
        InputPerMillion >= 0 && OutputPerMillion >= 0 && CacheReadPerMillion >= 0 && CacheWritePerMillion >= 0;
}
