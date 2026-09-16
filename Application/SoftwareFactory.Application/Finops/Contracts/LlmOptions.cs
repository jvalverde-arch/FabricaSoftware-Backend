using System.Globalization;

namespace SoftwareFactory.Application.Finops.Contracts;

/// <summary>
/// The whole LLM configuration (doc 03, D3 and §6): which tier serves each task, which provider and model serve each
/// tier, what each model costs, and the hard budgets. Bound from the <c>Llm</c> configuration section.
/// </summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>Tier used by a task that is not listed in <see cref="Tasks"/>.</summary>
    public LlmTier DefaultTier { get; set; } = LlmTier.Medium;

    public IDictionary<string, LlmTier> Tasks { get; } = new Dictionary<string, LlmTier>(StringComparer.OrdinalIgnoreCase);

    public IDictionary<LlmTier, LlmTierOptions> Tiers { get; } = new Dictionary<LlmTier, LlmTierOptions>();

    public IDictionary<string, LlmModelPricing> Models { get; } = new Dictionary<string, LlmModelPricing>(StringComparer.OrdinalIgnoreCase);

    public IDictionary<string, LlmProviderOptions> Providers { get; } = new Dictionary<string, LlmProviderOptions>(StringComparer.OrdinalIgnoreCase);

    public LlmBudgetOptions Budget { get; init; } = new();

    public LlmResolution Resolve(string task)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(task);

        var tier = Tasks.TryGetValue(task, out var mapped) ? mapped : DefaultTier;

        if (!Tiers.TryGetValue(tier, out var tierOptions))
        {
            throw new InvalidOperationException(Message($"tier '{tier}' (task '{task}') is not configured under Llm:Tiers"));
        }

        if (!Models.TryGetValue(tierOptions.Model, out var pricing))
        {
            throw new InvalidOperationException(Message($"model '{tierOptions.Model}' has no pricing under Llm:Models"));
        }

        return new LlmResolution(tier, tierOptions.Provider, tierOptions.Model, tierOptions.MaxOutputTokens, pricing);
    }

    public bool IsValid() =>
        Tiers.Count > 0
        && Budget.IsValid()
        && Models.Values.All(pricing => pricing.IsValid())
        && Tiers.Values.All(tier =>
            !string.IsNullOrWhiteSpace(tier.Provider)
            && !string.IsNullOrWhiteSpace(tier.Model)
            && tier.MaxOutputTokens > 0
            && Providers.ContainsKey(tier.Provider)
            && Models.ContainsKey(tier.Model))
        && Tasks.Values.All(Tiers.ContainsKey);

    private static string Message(string detail) =>
        string.Create(CultureInfo.InvariantCulture, $"The LLM configuration is incomplete: {detail}.");
}
