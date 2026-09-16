namespace SoftwareFactory.Application.Finops.Contracts;

/// <summary>Which provider and model serve a tier. Changing model or vendor is configuration, never code (doc 03 §6).</summary>
public sealed class LlmTierOptions
{
    public string Provider { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    /// <summary>Default ceiling for the answer when the caller does not set one.</summary>
    public int MaxOutputTokens { get; set; } = 4_096;
}
