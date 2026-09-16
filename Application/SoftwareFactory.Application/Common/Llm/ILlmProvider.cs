namespace SoftwareFactory.Application.Common.Llm;

/// <summary>
/// Port to one LLM vendor (doc 03, D3: «ningún agente conoce al proveedor»). Implementations live in Infrastructure
/// and are selected by <see cref="Name"/>, which is the key used by the tier configuration.
/// </summary>
public interface ILlmProvider
{
    /// <summary>Configuration key of this provider, e.g. <c>anthropic</c> or <c>local</c>.</summary>
    string Name { get; }

    Task<LlmProviderResponse> CompleteAsync(LlmProviderRequest request, CancellationToken cancellationToken);
}
