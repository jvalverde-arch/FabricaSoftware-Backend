namespace SoftwareFactory.Application.Finops.Contracts;

/// <summary>Wire protocol of a configured provider. OpenAI-compatible covers vLLM and Ollama (doc 03, D3).</summary>
public enum LlmProviderKind
{
    Anthropic,
    OpenAiCompatible,
}
