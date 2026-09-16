namespace SoftwareFactory.Domain.Platform;

/// <summary>
/// Progressive lockout curve of estandar-auth.md §2: every <see cref="Threshold"/> consecutive failures lock the account for
/// <see cref="BaseDuration"/>, doubling with each further block and never exceeding <see cref="MaximumDuration"/> (the cap keeps
/// a third party from locking an account forever). The values come from configuration; <see cref="Default"/> is the standard.
/// </summary>
public sealed record LockoutPolicy
{
    /// <summary>Beyond this many blocks the doubling is already past any sane cap; it also keeps the shift from overflowing.</summary>
    private const int MaximumExponent = 30;

    public LockoutPolicy(int threshold, TimeSpan baseDuration, TimeSpan maximumDuration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threshold, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(baseDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDuration, baseDuration);

        Threshold = threshold;
        BaseDuration = baseDuration;
        MaximumDuration = maximumDuration;
    }

    public static LockoutPolicy Default { get; } = new(5, TimeSpan.FromMinutes(1), TimeSpan.FromDays(1));

    /// <summary>Consecutive failures that make up one block.</summary>
    public int Threshold { get; }

    public TimeSpan BaseDuration { get; }

    public TimeSpan MaximumDuration { get; }

    /// <summary>Lockout length after <paramref name="block"/> complete blocks of failures (the first block is 1).</summary>
    public TimeSpan DurationForBlock(int block)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(block, 1);

        var exponent = Math.Min(block - 1, MaximumExponent);
        var duration = BaseDuration * Math.Pow(2, exponent);
        return duration > MaximumDuration ? MaximumDuration : duration;
    }
}
