using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Common.Llm;
using SoftwareFactory.Application.Common.Time;
using SoftwareFactory.Application.Finops;
using SoftwareFactory.Application.Finops.Contracts;

namespace SoftwareFactory.Infrastructure.Llm;

public static class LlmServiceCollectionExtensions
{
    /// <summary>
    /// Registers one adapter per configured provider (doc 03, D3) plus the gateway that measures and records every
    /// call. Each provider gets its own pooled <see cref="HttpClient"/> so timeouts and headers do not leak between them.
    /// </summary>
    public static IServiceCollection AddLlm(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMonotonicClock, StopwatchMonotonicClock>();
        services.AddHttpClient();

        services.AddSingleton<IEnumerable<ILlmProvider>>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<LlmOptions>>().Value;
            var factory = provider.GetRequiredService<IHttpClientFactory>();

            return
            [
                .. options.Providers.Select(entry => Create(entry.Key, entry.Value, factory.CreateClient($"llm:{entry.Key}"))),
            ];
        });

        services.AddScoped<ILlmGateway, LlmGateway>();

        return services;
    }

    private static ILlmProvider Create(string name, LlmProviderOptions options, HttpClient httpClient) => options.Kind switch
    {
        LlmProviderKind.Anthropic => new AnthropicLlmProvider(name, options, httpClient),
        LlmProviderKind.OpenAiCompatible => new OpenAiCompatibleLlmProvider(name, options, httpClient),
        _ => throw new InvalidOperationException($"Provider '{name}' declares an unknown kind '{options.Kind}'."),
    };
}
