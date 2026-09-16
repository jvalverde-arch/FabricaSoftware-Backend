using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Common.Llm;
using SoftwareFactory.Application.Finops.Contracts;

namespace SoftwareFactory.Api.Tests.Integration;

/// <summary>The LLM configuration of the host resolves end to end: providers, tiers, prices and the gateway.</summary>
[Collection(ApiCollectionDefinition.Name)]
public sealed class LlmCompositionTests(ApiFixture fixture)
{
    [Fact]
    public void The_host_resolves_the_gateway_and_one_provider_per_configured_entry()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var options = services.GetRequiredService<IOptions<LlmOptions>>().Value;
        var providers = services.GetRequiredService<IEnumerable<ILlmProvider>>().ToList();

        Assert.NotNull(services.GetRequiredService<ILlmGateway>());
        Assert.Equal(options.Providers.Count, providers.Count);
        Assert.Equal(options.Providers.Keys.Order(), providers.Select(provider => provider.Name).Order());
    }

    [Theory]
    [InlineData("artifact_generation", LlmTier.Large)]
    [InlineData("rubric_evaluation", LlmTier.Medium)]
    [InlineData("classification", LlmTier.Small)]
    [InlineData("a_task_nobody_mapped", LlmTier.Medium)]
    public void Every_task_of_the_default_configuration_resolves_to_a_priced_model(string task, LlmTier expected)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<LlmOptions>>().Value;

        var resolution = options.Resolve(task);

        Assert.Equal(expected, resolution.Tier);
        Assert.False(string.IsNullOrWhiteSpace(resolution.Model));
        Assert.True(resolution.Pricing.OutputPerMillion > 0, "a vendor model must carry its price");
        Assert.True(resolution.MaxOutputTokens > 0);
    }
}
