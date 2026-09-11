namespace SoftwareFactory.Application.Platform.Contracts;

/// <summary>Session settings (estandar-auth.md §1). Bound from the <c>Auth</c> configuration section.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Sliding lifetime of a refresh token; renewed on every rotation.</summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);

    /// <summary>Minimum password length (estandar-auth.md §2: 12, no arbitrary complexity rules).</summary>
    public int MinimumPasswordLength { get; set; } = 12;
}
