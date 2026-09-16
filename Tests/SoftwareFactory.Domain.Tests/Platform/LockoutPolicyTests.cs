using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Domain.Tests.Platform;

/// <summary>The lockout curve is configuration, not a constant: threshold, base duration and cap all come from settings.</summary>
public sealed class LockoutPolicyTests
{
    [Fact]
    public void Default_policy_matches_the_standard()
    {
        var policy = LockoutPolicy.Default;

        Assert.Equal(5, policy.Threshold);
        Assert.Equal(TimeSpan.FromMinutes(1), policy.BaseDuration);
        Assert.Equal(TimeSpan.FromDays(1), policy.MaximumDuration);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(5, 0, 1)]
    [InlineData(5, 1, 0)]
    public void Invalid_settings_are_rejected(int threshold, int baseMinutes, int maxMinutes) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LockoutPolicy(threshold, TimeSpan.FromMinutes(baseMinutes), TimeSpan.FromMinutes(maxMinutes)));

    [Fact]
    public void The_cap_must_not_be_shorter_than_the_base_duration() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new LockoutPolicy(5, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(5)));

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    public void Duration_doubles_with_each_block(int block, int expectedMinutes) =>
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), LockoutPolicy.Default.DurationForBlock(block));

    [Fact]
    public void Duration_never_exceeds_the_configured_cap()
    {
        var policy = new LockoutPolicy(3, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(8), policy.DurationForBlock(4));
        Assert.Equal(TimeSpan.FromMinutes(10), policy.DurationForBlock(5));
        Assert.Equal(TimeSpan.FromMinutes(10), policy.DurationForBlock(500));
    }
}
