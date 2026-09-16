using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SoftwareFactory.Application.Common.Llm;
using SoftwareFactory.Application.Finops.Contracts;

namespace SoftwareFactory.Infrastructure.Llm;

/// <summary>
/// Adapter for any endpoint that speaks the OpenAI chat-completions format: Azure OpenAI, vLLM and Ollama among them
/// (doc 03, D3). Plain HTTP on purpose — the whole point is to talk to whatever the client already runs.
/// </summary>
internal sealed class OpenAiCompatibleLlmProvider : ILlmProvider
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;

    public OpenAiCompatibleLlmProvider(string name, LlmProviderOptions options, HttpClient httpClient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);

        Name = name;
        _httpClient = httpClient;
        var baseUrl = options.BaseUrl
            ?? throw new InvalidOperationException($"Provider '{name}' is OpenAI-compatible and needs Llm:Providers:{name}:BaseUrl.");

        // Without the trailing slash the last path segment (for example /v1) is dropped when the relative path is resolved.
        _httpClient.BaseAddress = baseUrl.AbsoluteUri.EndsWith('/') ? baseUrl : new Uri(baseUrl.AbsoluteUri + "/");
        _httpClient.Timeout = options.Timeout;

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }
    }

    public string Name { get; }

    public async Task<LlmProviderResponse> CompleteAsync(LlmProviderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<ChatMessage> messages = string.IsNullOrWhiteSpace(request.System)
            ? []
            : [new ChatMessage("system", request.System)];
        messages.AddRange(request.Messages.Select(message => new ChatMessage(message.Role == LlmRole.Assistant ? "assistant" : "user", message.Content)));

        var payload = new ChatCompletionRequest(request.Model, messages, request.MaxOutputTokens, request.Temperature);

        // The base address already carries the API prefix (for example http://localhost:11434/v1).
        using var response = await _httpClient
            .PostAsJsonAsync(new Uri("chat/completions", UriKind.Relative), payload, _json, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(_json, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Provider '{Name}' returned an empty body.");

        var choice = completion.Choices.Count > 0
            ? completion.Choices[0]
            : throw new InvalidOperationException($"Provider '{Name}' returned no choices.");

        var usage = completion.Usage ?? new ChatUsage(0, 0, null);

        return new LlmProviderResponse(
            choice.Message.Content ?? string.Empty,
            completion.Model ?? request.Model,
            new LlmUsage(usage.PromptTokens, usage.CompletionTokens, usage.PromptTokensDetails?.CachedTokens ?? 0, CacheWriteTokens: 0),
            choice.FinishReason);
    }

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("temperature")] decimal? Temperature);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string? Content);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatChoice> Choices,
        [property: JsonPropertyName("usage")] ChatUsage? Usage);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatMessage Message,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);

    private sealed record ChatUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens,
        [property: JsonPropertyName("prompt_tokens_details")] ChatPromptTokensDetails? PromptTokensDetails);

    private sealed record ChatPromptTokensDetails(
        [property: JsonPropertyName("cached_tokens")] int CachedTokens);
}
