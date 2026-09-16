namespace SoftwareFactory.Application.Finops.Contracts;

/// <summary>Hard budgets of T-006: one call and one job may never cost more than this (doc 01, E11: límites duros).</summary>
public sealed class LlmBudgetOptions
{
    public decimal MaxCostPerCall { get; set; } = 1m;

    public decimal MaxCostPerJob { get; set; } = 10m;

    public bool IsValid() => MaxCostPerCall > 0 && MaxCostPerJob >= MaxCostPerCall;
}
