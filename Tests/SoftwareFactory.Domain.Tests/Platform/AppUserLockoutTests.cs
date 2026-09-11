using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Domain.Tests.Platform;

/// <summary>Progressive lockout of estandar-auth.md §2: every 5 failed attempts lock the account for 1 minute, doubling each time.</summary>
public sealed class AppUserLockoutTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Four_failures_do_not_lock_the_account()
    {
        var user = NewUser();

        for (var i = 0; i < 4; i++)
        {
            Assert.Null(user.RecordFailedAccess(_now));
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
    public void Lockout_never_exceeds_one_day()
    {
        var user = NewUser();

        var lockoutEnd = Fail(user, 5 * 20);

        Assert.Equal(_now.AddDays(1), lockoutEnd);
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
            last = user.RecordFailedAccess(_now);
        }

        return last;
    }

    private static AppUser NewUser() => new(Guid.CreateVersion7(), "user@tenant.test", "User", "$argon2id$v=19$m=8,t=1,p=1$c2FsdA$aGFzaA");
}
