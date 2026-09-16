namespace SoftwareFactory.Api.Security;

/// <summary>Fixed-window limit per client address on every <c>/api/auth</c> endpoint (estandar-auth.md §3). Section <c>Auth:RateLimit</c>.</summary>
public sealed class AuthRateLimitOptions
{
    public const string SectionName = "Auth:RateLimit";

    public int PermitLimit { get; set; } = 10;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}
