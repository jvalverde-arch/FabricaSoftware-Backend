using SoftwareFactory.Application.Common.Llm;
using SoftwareFactory.Application.Finops.Contracts;

namespace SoftwareFactory.Application.Finops;

/// <summary>
/// Turns tokens into money (doc 01, E11). Prices are per million tokens; the result is rounded to the six decimals of
/// the <c>numeric(12,6)</c> column, away from zero, so the platform never under-charges itself by a rounding step.
/// </summary>
public static class LlmCostCalculator
{
    private const int Decimals = 6;
    private const decimal Million = 1_000_000m;

    public static decimal Compute(LlmUsage usage, LlmModelPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(pricing);

        var total =
            ((usage.InputTokens * pricing.InputPerMillion)
             + (usage.OutputTokens * pricing.OutputPerMillion)
             + (usage.CacheReadTokens * pricing.CacheReadPerMillion)
             + (usage.CacheWriteTokens * pricing.CacheWritePerMillion))
            / Million;

        return Round(total);
    }

    /// <summary>
    /// Upper bound of a call before making it: the input the caller is about to send plus the whole output ceiling.
    /// Cache tokens are ignored because they can only make the real cost lower.
    /// </summary>
    public static decimal EstimateWorstCase(int inputTokens, int maxOutputTokens, LlmModelPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(pricing);
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(maxOutputTokens);

        return Round(((inputTokens * pricing.InputPerMillion) + (maxOutputTokens * pricing.OutputPerMillion)) / Million);
    }

    // Adding a zero of the target scale normalizes the representation to six decimals (0.0059 -> 0.005900),
    // so what is logged and returned reads the same as the numeric(12,6) column.
    private static decimal Round(decimal value) => Math.Round(value, Decimals, MidpointRounding.AwayFromZero) + 0.000000m;
}
