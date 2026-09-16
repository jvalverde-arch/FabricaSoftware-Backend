using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Domain.Tests.Platform;

/// <summary>State machine of an agent run (doc 03, D6): claim, progress, success, retry with backoff, give up.</summary>
public sealed class JobTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly JobRetryPolicy _policy = JobRetryPolicy.Default;

    [Fact]
    public void A_new_job_is_pending_and_available_right_away()
    {
        var job = NewJob();

        Assert.Equal(JobState.Pending, job.State);
        Assert.Equal(0, job.Attempts);
        Assert.Equal(job.CreatedAt, job.AvailableAt);
        Assert.Null(job.LockedUntil);
        Assert.Null(job.Phase);
    }

    [Fact]
    public void Starting_a_job_counts_the_attempt_and_takes_a_lease()
    {
        var job = NewJob();

        job.Start(_now, lease: TimeSpan.FromMinutes(5));

        Assert.Equal(JobState.Running, job.State);
        Assert.Equal(1, job.Attempts);
        Assert.Equal(_now, job.StartedAt);
        Assert.Equal(_now.AddMinutes(5), job.LockedUntil);
    }

    [Fact]
    public void Progress_is_a_phase_and_an_optional_percentage()
    {
        var job = NewJob();
        job.Start(_now, TimeSpan.FromMinutes(5));

        job.ReportProgress("leyendo contexto", 10, _now.AddSeconds(3), TimeSpan.FromMinutes(5));

        Assert.Equal("leyendo contexto", job.Phase);
        Assert.Equal(10, job.ProgressPercent);
        Assert.Equal(_now.AddSeconds(3), job.UpdatedAt);
        // Reporting progress renews the lease: the job is alive.
        Assert.Equal(_now.AddSeconds(3).AddMinutes(5), job.LockedUntil);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void A_percentage_outside_the_scale_is_rejected(int percent)
    {
        var job = NewJob();
        job.Start(_now, TimeSpan.FromMinutes(5));

        Assert.Throws<ArgumentOutOfRangeException>(() => job.ReportProgress("fase", percent, _now, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void A_finished_job_releases_its_lease_and_reaches_one_hundred()
    {
        var job = NewJob();
        job.Start(_now, TimeSpan.FromMinutes(5));

        job.Succeed(_now.AddSeconds(30));

        Assert.Equal(JobState.Succeeded, job.State);
        Assert.Equal(_now.AddSeconds(30), job.CompletedAt);
        Assert.Equal(100, job.ProgressPercent);
        Assert.Null(job.LockedUntil);
        Assert.Null(job.LastError);
    }

    [Fact]
    public void A_failure_that_can_be_retried_goes_back_to_pending_with_exponential_backoff()
    {
        var job = NewJob();
        job.Start(_now, TimeSpan.FromMinutes(5));

        var retried = job.Fail("timeout del proveedor", _now.AddSeconds(10), _policy);

        Assert.True(retried);
        Assert.Equal(JobState.Pending, job.State);
        Assert.Equal("timeout del proveedor", job.LastError);
        Assert.Null(job.LockedUntil);
        Assert.Null(job.CompletedAt);
        // First retry: base delay of the policy.
        Assert.Equal(_now.AddSeconds(10).Add(_policy.BaseDelay), job.AvailableAt);
    }

    [Fact]
    public void Each_further_failure_waits_twice_as_long_up_to_the_cap()
    {
        var policy = new JobRetryPolicy(MaxAttempts: 10, BaseDelay: TimeSpan.FromSeconds(10), MaxDelay: TimeSpan.FromMinutes(1));
        var job = NewJob();
        var delays = new List<TimeSpan>();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            job.Start(_now, TimeSpan.FromMinutes(5));
            job.Fail("fallo", _now, policy);
            delays.Add(job.AvailableAt - _now);
        }

        Assert.Equal(
            [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1)],
            delays);
    }

    [Fact]
    public void After_the_last_attempt_the_job_is_failed_for_good()
    {
        var policy = new JobRetryPolicy(MaxAttempts: 2, BaseDelay: TimeSpan.FromSeconds(5), MaxDelay: TimeSpan.FromMinutes(1));
        var job = NewJob();

        job.Start(_now, TimeSpan.FromMinutes(5));
        Assert.True(job.Fail("primero", _now, policy));

        job.Start(_now.AddSeconds(5), TimeSpan.FromMinutes(5));
        var retried = job.Fail("segundo", _now.AddSeconds(6), policy);

        Assert.False(retried);
        Assert.Equal(JobState.Failed, job.State);
        Assert.Equal("segundo", job.LastError);
        Assert.Equal(_now.AddSeconds(6), job.CompletedAt);
        Assert.Null(job.LockedUntil);
    }

    [Fact]
    public void A_job_can_be_cancelled_while_it_waits_but_not_after_it_finished()
    {
        var job = NewJob();

        job.Cancel(_now);

        Assert.Equal(JobState.Cancelled, job.State);
        Assert.Equal(_now, job.CompletedAt);

        var finished = NewJob();
        finished.Start(_now, TimeSpan.FromMinutes(5));
        finished.Succeed(_now);
        Assert.Throws<InvalidOperationException>(() => finished.Cancel(_now));
    }

    [Fact]
    public void The_lease_says_when_a_crashed_run_can_be_taken_over()
    {
        var job = NewJob();
        job.Start(_now, TimeSpan.FromMinutes(5));

        Assert.False(job.IsLeaseExpired(_now.AddMinutes(4)));
        Assert.True(job.IsLeaseExpired(_now.AddMinutes(5).AddSeconds(1)));

        // Taking it over puts it back in the queue without consuming another attempt beyond the one it already used.
        job.ReleaseExpiredLease(_now.AddMinutes(6));

        Assert.Equal(JobState.Pending, job.State);
        Assert.Equal(1, job.Attempts);
        Assert.Equal(_now.AddMinutes(6), job.AvailableAt);
        Assert.Null(job.LockedUntil);
    }

    [Fact]
    public void Only_a_running_job_reports_progress_or_finishes()
    {
        var pending = NewJob();

        Assert.Throws<InvalidOperationException>(() => pending.Succeed(_now));
        Assert.Throws<InvalidOperationException>(() => pending.ReportProgress("fase", null, _now, TimeSpan.FromMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => pending.Fail("x", _now, _policy));
    }

    private static Job NewJob() => new(Guid.CreateVersion7(), "probe", """{"n":1}""");
}
