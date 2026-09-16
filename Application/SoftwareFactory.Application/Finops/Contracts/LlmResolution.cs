namespace SoftwareFactory.Application.Finops.Contracts;

/// <summary>Result of resolving a task: the tier it belongs to and the provider, model and prices that serve it.</summary>
public sealed record LlmResolution(LlmTier Tier, string Provider, string Model, int MaxOutputTokens, LlmModelPricing Pricing);
