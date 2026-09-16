using SoftwareFactory.Application.Finops.Contracts;

namespace SoftwareFactory.Application.Tests.Finops;

/// <summary>Task → tier → model is configuration, never code (doc 03, D3 and §6).</summary>
public sealed class LlmOptionsTests
{
    [Fact]
    public void Resolves_a_task_to_its_tier_provider_model_and_pricing()
    {
        var options = Sample();

        var resolved = options.Resolve("artifact_generation");

        Assert.Equal(LlmTier.Large, resolved.Tier);
        Assert.Equal("anthropic", resolved.Provider);
        Assert.Equal("claude-sonnet-5", resolved.Model);
        Assert.Equal(2m, resolved.Pricing.InputPerMillion);
        Assert.Equal(10m, resolved.Pricing.OutputPerMillion);
    }

    [Fact]
    public void An_unmapped_task_falls_back_to_the_default_tier()
    {
        var options = Sample();

        var resolved = options.Resolve("something_new");

        Assert.Equal(LlmTier.Medium, resolved.Tier);
        Assert.Equal("claude-haiku-4-5", resolved.Model);
    }

    [Fact]
    public void An_unknown_tier_or_missing_pricing_is_a_configuration_error()
    {
        var withoutTier = Sample();
        withoutTier.Tiers.Remove(LlmTier.Medium);
        Assert.Throws<InvalidOperationException>(() => withoutTier.Resolve("something_new"));

        var withoutPricing = Sample();
        withoutPricing.Models.Remove("claude-sonnet-5");
        Assert.Throws<InvalidOperationException>(() => withoutPricing.Resolve("artifact_generation"));
    }

    [Fact]
    public void Validation_requires_every_tier_to_point_at_a_known_provider_and_priced_model()
    {
        Assert.True(Sample().IsValid());

        var unknownProvider = Sample();
        unknownProvider.Tiers[LlmTier.Large] = new LlmTierOptions { Provider = "ghost", Model = "claude-sonnet-5" };
        Assert.False(unknownProvider.IsValid());

        var unpricedModel = Sample();
        unpricedModel.Tiers[LlmTier.Large] = new LlmTierOptions { Provider = "anthropic", Model = "not-priced" };
        Assert.False(unpricedModel.IsValid());

        var noBudget = Sample();
        noBudget.Budget.MaxCostPerCall = 0m;
        Assert.False(noBudget.IsValid());
    }

    internal static LlmOptions Sample() => new()
    {
        DefaultTier = LlmTier.Medium,
        Tasks = { ["artifact_generation"] = LlmTier.Large, ["classification"] = LlmTier.Small },
        Tiers =
        {
            [LlmTier.Large] = new LlmTierOptions { Provider = "anthropic", Model = "claude-sonnet-5" },
            [LlmTier.Medium] = new LlmTierOptions { Provider = "anthropic", Model = "claude-haiku-4-5" },
            [LlmTier.Small] = new LlmTierOptions { Provider = "local", Model = "qwen3-8b" },
        },
        Models =
        {
            ["claude-sonnet-5"] = new LlmModelPricing { InputPerMillion = 2m, OutputPerMillion = 10m, CacheReadPerMillion = 0.2m, CacheWritePerMillion = 2.5m },
            ["claude-haiku-4-5"] = new LlmModelPricing { InputPerMillion = 1m, OutputPerMillion = 5m, CacheReadPerMillion = 0.1m, CacheWritePerMillion = 1.25m },
            ["qwen3-8b"] = new LlmModelPricing(),
        },
        Providers = { ["anthropic"] = new LlmProviderOptions(), ["local"] = new LlmProviderOptions { Kind = LlmProviderKind.OpenAiCompatible, BaseUrl = new Uri("http://localhost:11434/v1") } },
        Budget = new LlmBudgetOptions { MaxCostPerCall = 1m, MaxCostPerJob = 10m },
    };
}
