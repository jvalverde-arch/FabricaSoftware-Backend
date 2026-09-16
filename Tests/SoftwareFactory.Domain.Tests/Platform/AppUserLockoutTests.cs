using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Domain.Tests.Platform;

/// <summary>Progressive lockout of estandar-auth.md §2: every N failed attempts lock the account, doubling each block, capped by configuration.</summary>
public sealed class AppUserLockoutTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    private static readonly LockoutPolicy _policy = LockoutPolicy.Default;

    [Fact]
    public void Four_failures_do_not_lock_the_account()
    {
        var user = NewUser();

        for (var i = 0; i < 4; i++)
        {
            Assert.Null(user.RecordFailedAccess(_policy, _now));
        }

        Assert.Equal(4, user.FailedAccessCount);
        Assert.False(user.IsLockedOut(_now));
    }

    [Fact]
    public void Fifth_failure_locks_for_one_minute()
    {
        var user = NewUser();

        var lockoutEnd = Fail(user, 5);

        Assert.Equal(_now.AddMinutes(1), lockoutEnd);
        Assert.True(user.IsLockedOut(_now));
        Assert.True(user.IsLockedOut(_now.AddSeconds(59)));
        Assert.False(user.IsLockedOut(_now.AddMinutes(1)));
    }

    [Theory]
    [InlineData(10, 2)]
    [InlineData(15, 4)]
    [InlineData(20, 8)]
    public void Each_further_block_of_five_failures_doubles_the_lockout(int failures, int expectedMinutes)
    {
        var user = NewUser();

        var lockoutEnd = Fail(user, failures);

        Assert.Equal(_now.AddMinutes(expectedMinutes), lockoutEnd);
    }

    [Fact]
    public void Lockout_never_exceeds_the_configured_cap()
    {
        var user = NewUser();

        var lockoutEnd = Fail(user, LockoutPolicy.Default.Threshold * 20);

        Assert.Equal(_now.Add(LockoutPolicy.Default.MaximumDuration), lockoutEnd);
    }

    [Fact]
    public void A_shorter_configured_cap_is_honoured()
    {
        var user = NewUser();
        var policy = new LockoutPolicy(2, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2));

        DateTimeOffset? last = null;

        for (var i = 0; i < 12; i++)
        {
            last = user.RecordFailedAccess(policy, _now);
        }

        Assert.Equal(_now.AddMinutes(2), last);
    }

    [Fact]
    public void Successful_access_resets_failures_and_lockout()
    {
        var user = NewUser();
        Fail(user, 5);

        user.ResetFailedAccess();

        Assert.Equal(0, user.FailedAccessCount);
        Assert.Null(user.LockoutEnd);
        Assert.False(user.IsLockedOut(_now));
    }

    private static DateTimeOffset? Fail(AppUser user, int times)
    {
        DateTimeOffset? last = null;

        for (var i = 0; i < times; i++)
        {
            last = user.RecordFailedAccess(_policy, _now);
        }

        return last;
    }

    private static AppUser NewUser() => new(Guid.CreateVersion7(), "user@tenant.test", "User", "$argon2id$v=19$m=8,t=1,p=1$c2FsdA$aGFzaA");
}
