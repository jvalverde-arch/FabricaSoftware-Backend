using System.Globalization;
using Xunit.Abstractions;
using SoftwareFactory.Application.Common.Llm;
using SoftwareFactory.Application.Finops;
using SoftwareFactory.Application.Finops.Contracts;
using SoftwareFactory.Infrastructure.Llm;

namespace SoftwareFactory.Infrastructure.Tests.Llm;

/// <summary>
/// The other half of the T-006 criterion: the adapters against a real endpoint. Excluded from the default run
/// (see <c>tests.runsettings</c>) because it needs credentials or a local model, and costs money with a vendor.
/// <code>
/// # Anthropic
/// SF_LLM_KIND=anthropic SF_LLM_MODEL=claude-haiku-4-5 SF_LLM_API_KEY=sk-ant-... \
///   dotnet test Tests/SoftwareFactory.Infrastructure.Tests --filter Category=RealEndpoint
/// # OpenAI-compatible (Ollama, vLLM)
/// SF_LLM_KIND=openai SF_LLM_BASE_URL=http://localhost:11434/v1 SF_LLM_MODEL=qwen2.5:0.5b \
///   dotnet test Tests/SoftwareFactory.Infrastructure.Tests --filter Category=RealEndpoint
/// </code>
/// </summary>
[Trait("Category", "RealEndpoint")]
public sealed class RealEndpointLlmProviderTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Answers_and_reports_the_tokens_it_spent()
    {
        var kind = Required("SF_LLM_KIND");
        var model = Required("SF_LLM_MODEL");
        var options = new LlmProviderOptions
        {
            Kind = string.Equals(kind, "anthropic", StringComparison.OrdinalIgnoreCase) ? LlmProviderKind.Anthropic : LlmProviderKind.OpenAiCompatible,
            ApiKey = Environment.GetEnvironmentVariable("SF_LLM_API_KEY"),
            BaseUrl = Environment.GetEnvironmentVariable("SF_LLM_BASE_URL") is { Length: > 0 } baseUrl ? new Uri(baseUrl) : null,
            Timeout = TimeSpan.FromMinutes(2),
        };

        ILlmProvider provider = options.Kind == LlmProviderKind.Anthropic
            ? new AnthropicLlmProvider("anthropic", options, new HttpClient())
            : new OpenAiCompatibleLlmProvider("local", options, new HttpClient());

        var response = await provider.CompleteAsync(
            new LlmProviderRequest
            {
                Model = model,
                System = "Responde con una sola palabra, en español y en minúsculas.",
                Messages = [LlmMessage.User("¿De qué color es el cielo despejado al mediodía?")],
                MaxOutputTokens = 64,
            },
            CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
        Assert.True(response.Usage.InputTokens > 0, "the endpoint must report input tokens");
        Assert.True(response.Usage.OutputTokens > 0, "the endpoint must report output tokens");

        // The measured cost of a real answer, with the prices of the configuration.
        var pricing = new LlmModelPricing { InputPerMillion = 1m, OutputPerMillion = 5m };
        var cost = LlmCostCalculator.Compute(response.Usage, pricing);
        Assert.True(cost >= 0m);

        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"real endpoint {kind}/{model}: {response.Usage.InputTokens} in / {response.Usage.OutputTokens} out, cost {cost}, text «{response.Text.Trim()}»"));
    }

    private static string Required(string variable) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{variable} is required to run the real-endpoint test (see the class documentation).");
}
