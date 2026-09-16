using Anthropic;
using Anthropic.Models.Messages;
using SoftwareFactory.Application.Common.Llm;
using SoftwareFactory.Application.Finops.Contracts;
using LlmMessage = SoftwareFactory.Application.Common.Llm.LlmMessage;

namespace SoftwareFactory.Infrastructure.Llm;

/// <summary>
/// Adapter over the official Anthropic SDK (doc 03, D3). It maps the platform's provider-neutral request to the
/// Messages API and reports the token counts the API returns, including the cache counters that feed the cost.
/// </summary>
internal sealed class AnthropicLlmProvider : ILlmProvider, IDisposable
{
    /// <summary>Public Anthropic endpoint; overridden by configuration for a gateway or a proxy.</summary>
    private const string DefaultBaseUrl = "https://api.anthropic.com";

    private readonly AnthropicClient _client;

    public AnthropicLlmProvider(string name, LlmProviderOptions options, HttpClient httpClient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);

        Name = name;
        _client = new AnthropicClient
        {
            ApiKey = options.ApiKey ?? string.Empty,
            HttpClient = httpClient,
            Timeout = options.Timeout,
            BaseUrl = options.BaseUrl?.ToString() ?? DefaultBaseUrl,
        };
    }

    public string Name { get; }

    public void Dispose() => _client.Dispose();

    public async Task<LlmProviderResponse> CompleteAsync(LlmProviderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Temperature is deliberately not sent: models released after Claude Opus 4.6 reject any value but 1.0.
        var parameters = new MessageCreateParams
        {
            Model = request.Model,
            MaxTokens = request.MaxOutputTokens,
            Messages = [.. request.Messages.Select(ToMessageParam)],
        };

        if (!string.IsNullOrWhiteSpace(request.System))
        {
            parameters = parameters with { System = request.System };
        }

        var message = await _client.Messages.Create(parameters, cancellationToken: cancellationToken).ConfigureAwait(false);

        var text = string.Join(
            '\n',
            message.Content.Select(block => block.Value).OfType<TextBlock>().Select(block => block.Text));

        // `Model` and `StopReason` are string-backed API enums: `Raw` is the wire value, `ToString()` would quote it.
        return new LlmProviderResponse(text, message.Model.Raw() ?? request.Model, UsageOf(message.Usage), message.StopReason?.Raw());
    }

    private static MessageParam ToMessageParam(LlmMessage message) => new()
    {
        Role = message.Role == LlmRole.Assistant ? Role.Assistant : Role.User,
        Content = message.Content,
    };

    private static LlmUsage UsageOf(Usage usage) => new(
        ToInt(usage.InputTokens),
        ToInt(usage.OutputTokens),
        ToInt(usage.CacheReadInputTokens),
        ToInt(usage.CacheCreationInputTokens));

    private static int ToInt(long? value) => value is null ? 0 : (int)Math.Clamp(value.Value, 0, int.MaxValue);
}
