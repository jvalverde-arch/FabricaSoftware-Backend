using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Platform.Contracts;

/// <summary>Session and credential settings (estandar-auth.md §1-§2). Bound from the <c>Auth</c> configuration section.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Sliding lifetime of a refresh token; renewed on every rotation.</summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);

    /// <summary>Minimum password length (estandar-auth.md §2: 12, no arbitrary complexity rules).</summary>
    public int MinimumPasswordLength { get; set; } = 12;

    public LockoutOptions Lockout { get; init; } = new();

    public bool IsValid() => MinimumPasswordLength >= 12 && RefreshTokenLifetime > TimeSpan.Zero && Lockout.IsValid();

    /// <summary>Builds the domain policy from the configured values.</summary>
    public LockoutPolicy LockoutPolicy() => new(Lockout.Threshold, Lockout.BaseDuration, Lockout.MaximumDuration);
}

/// <summary>Progressive lockout settings; the cap keeps a third party from locking an account indefinitely.</summary>
public sealed class LockoutOptions
{
    public int Threshold { get; set; } = 5;

    public TimeSpan BaseDuration { get; set; } = TimeSpan.FromMinutes(1);

    public TimeSpan MaximumDuration { get; set; } = TimeSpan.FromDays(1);

    public bool IsValid() => Threshold >= 1 && BaseDuration > TimeSpan.Zero && MaximumDuration >= BaseDuration;
}
