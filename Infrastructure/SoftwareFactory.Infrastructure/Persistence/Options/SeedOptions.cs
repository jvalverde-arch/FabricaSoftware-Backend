namespace SoftwareFactory.Infrastructure.Persistence.Options;

/// <summary>Development seed: the «local» tenant and its administrator (sprint-00, T-003).</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Fixed id of the local tenant so the seed is idempotent even under forced row-level security.</summary>
    public static Guid LocalTenantId { get; } = Guid.Parse("01920000-0000-7000-8000-000000000001");

    public string TenantName { get; set; } = "local";

    public string TenantSlug { get; set; } = "local";

    public string AdminEmail { get; set; } = "admin@local";

    public string AdminDisplayName { get; set; } = "Administrador";

    /// <summary>Never committed: comes from user-secrets, an Aspire parameter or an environment variable. Without it the admin is not created.</summary>
    public string? AdminPassword { get; set; }
}
