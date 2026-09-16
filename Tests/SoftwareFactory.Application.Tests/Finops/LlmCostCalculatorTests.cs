using SoftwareFactory.Application.Common.Llm;
using SoftwareFactory.Application.Finops;
using SoftwareFactory.Application.Finops.Contracts;

namespace SoftwareFactory.Application.Tests.Finops;

/// <summary>Cost measured per call (doc 01, E11). Prices are per million tokens; the column is numeric(12,6).</summary>
public sealed class LlmCostCalculatorTests
{
    private static readonly LlmModelPricing _sonnet = new()
    {
        InputPerMillion = 2m,
        OutputPerMillion = 10m,
        CacheReadPerMillion = 0.2m,
        CacheWritePerMillion = 2.5m,
    };

    [Fact]
    public void Adds_up_input_output_and_cache_tokens()
    {
        var usage = new LlmUsage(InputTokens: 1_000_000, OutputTokens: 500_000, CacheReadTokens: 2_000_000, CacheWriteTokens: 400_000);

        var cost = LlmCostCalculator.Compute(usage, _sonnet);

        // 2 + 5 + 0.4 + 1 = 8.4
        Assert.Equal(8.4m, cost);
    }

    [Fact]
    public void A_small_call_keeps_six_decimals_and_rounds_half_up()
    {
        var usage = new LlmUsage(InputTokens: 1_200, OutputTokens: 350, CacheReadTokens: 0, CacheWriteTokens: 0);

        var cost = LlmCostCalculator.Compute(usage, _sonnet);

        // 1200 * 2/1e6 = 0.0024 ; 350 * 10/1e6 = 0.0035
        Assert.Equal(0.005900m, cost);
        Assert.Equal(6, (decimal.GetBits(cost)[3] >> 16) & 0xFF);
    }

    [Fact]
    public void A_self_hosted_model_without_prices_costs_nothing()
    {
        var usage = new LlmUsage(InputTokens: 10_000, OutputTokens: 10_000, CacheReadTokens: 0, CacheWriteTokens: 0);

        Assert.Equal(0m, LlmCostCalculator.Compute(usage, new LlmModelPricing()));
    }

    [Fact]
    public void The_worst_case_of_a_call_is_estimated_before_spending_anything()
    {
        // The pre-flight check assumes the whole output budget is used and the input is what the caller sends.
        var estimate = LlmCostCalculator.EstimateWorstCase(inputTokens: 10_000, maxOutputTokens: 4_000, _sonnet);

        // 10000 * 2/1e6 = 0.02 ; 4000 * 10/1e6 = 0.04
        Assert.Equal(0.060000m, estimate);
    }
}
