namespace SoftwareFactory.Domain.Platform;

/// <summary>
/// Retry curve of a job (doc 03, D6): up to <paramref name="MaxAttempts"/> runs, waiting <paramref name="BaseDelay"/>
/// after the first failure and doubling with each further one, never beyond <paramref name="MaxDelay"/>.
/// </summary>
public sealed record JobRetryPolicy(int MaxAttempts, TimeSpan BaseDelay, TimeSpan MaxDelay)
{
    /// <summary>Beyond this the doubling is already past any sane cap and would overflow the shift.</summary>
    private const int MaximumExponent = 30;

    public static JobRetryPolicy Default { get; } = new(MaxAttempts: 3, BaseDelay: TimeSpan.FromSeconds(30), MaxDelay: TimeSpan.FromMinutes(15));

    /// <summary>Wait before the run that follows <paramref name="attempts"/> failed ones.</summary>
    public TimeSpan DelayAfter(int attempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);

        var delay = BaseDelay * Math.Pow(2, Math.Min(attempts - 1, MaximumExponent));
        return delay > MaxDelay ? MaxDelay : delay;
    }

    public bool IsValid() => MaxAttempts >= 1 && BaseDelay > TimeSpan.Zero && MaxDelay >= BaseDelay;
}
