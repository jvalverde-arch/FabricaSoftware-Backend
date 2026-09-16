namespace SoftwareFactory.Application.Finops.Contracts;

/// <summary>Connection settings of one provider. The api key never lives in the repository: vault or user-secrets (D9).</summary>
public sealed class LlmProviderOptions
{
    public LlmProviderKind Kind { get; set; } = LlmProviderKind.Anthropic;

    /// <summary>Overrides the vendor default; required for OpenAI-compatible endpoints (vLLM, Ollama).</summary>
    public Uri? BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}
