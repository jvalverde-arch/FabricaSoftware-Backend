using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Platform;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Application.Tests.Platform.Fakes;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Tests.Platform.Jobs;

/// <summary>What happens to a claimed run: it is executed, it reports progress, and it ends in success or retry.</summary>
public sealed class JobDispatcherTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Runs_the_handler_of_the_job_type_and_marks_it_succeeded()
    {
        var harness = new Harness();

        await harness.Dispatcher.ExecuteAsync(harness.Claimed, CancellationToken.None);

        Assert.Single(harness.Handler.Executions);
        Assert.Equal(harness.Job.Id, harness.Handler.Executions[0].JobId);
        Assert.Equal("""{"n":1}""", harness.Handler.Executions[0].Payload);
        Assert.Equal(JobState.Succeeded, harness.Job.State);
        Assert.Equal(100, harness.Job.ProgressPercent);
        Assert.True(harness.UnitOfWork.Commits >= 1);
    }

    [Fact]
    public async Task The_run_is_the_current_job_so_its_llm_calls_carry_the_id()
    {
        var harness = new Harness();
        Guid? seenInsideHandler = null;
        harness.Handler.OnHandle = _ =>
        {
            seenInsideHandler = harness.CurrentJob.JobId;
            return Task.CompletedTask;
        };

        await harness.Dispatcher.ExecuteAsync(harness.Claimed, CancellationToken.None);

        Assert.Equal(harness.Job.Id, seenInsideHandler);
    }

    [Fact]
    public async Task Progress_reported_by_the_handler_reaches_the_job()
    {
        var harness = new Harness();
        harness.Handler.OnHandle = async execution =>
        {
            await execution.ReportProgressAsync("leyendo contexto", 25, CancellationToken.None);
            await execution.ReportProgressAsync("generando", 60, CancellationToken.None);
        };

        await harness.Dispatcher.ExecuteAsync(harness.Claimed, CancellationToken.None);

        Assert.Equal(JobState.Succeeded, harness.Job.State);
        Assert.True(harness.UnitOfWork.Commits >= 3, "each progress report is persisted so the stream can see it");
    }

    [Fact]
    public async Task A_handler_that_throws_sends_the_job_back_to_the_queue_with_backoff()
    {
        var harness = new Harness();
        harness.Handler.OnHandle = _ => throw new InvalidOperationException("el proveedor falló");

        await harness.Dispatcher.ExecuteAsync(harness.Claimed, CancellationToken.None);

        Assert.Equal(JobState.Pending, harness.Job.State);
        Assert.Equal("el proveedor falló", harness.Job.LastError);
        Assert.Equal(_now.AddSeconds(30), harness.Job.AvailableAt);
    }

    [Fact]
    public async Task After_the_last_attempt_the_job_fails_for_good()
    {
        var harness = new Harness(maxAttempts: 1);
        harness.Handler.OnHandle = _ => throw new InvalidOperationException("otra vez");

        await harness.Dispatcher.ExecuteAsync(harness.Claimed, CancellationToken.None);

        Assert.Equal(JobState.Failed, harness.Job.State);
        Assert.NotNull(harness.Job.CompletedAt);
    }

    [Fact]
    public async Task A_job_type_without_handler_fails_without_retrying_forever()
    {
        var harness = new Harness(jobType: "unknown_type");

        await harness.Dispatcher.ExecuteAsync(harness.Claimed, CancellationToken.None);

        Assert.Equal(JobState.Failed, harness.Job.State);
        Assert.Contains("unknown_type", harness.Job.LastError, StringComparison.Ordinal);
        Assert.Empty(harness.Handler.Executions);
    }

    [Fact]
    public async Task A_cancelled_run_leaves_the_job_claimable_again_without_marking_it_failed()
    {
        var harness = new Harness();
        using var cancellation = new CancellationTokenSource();
        harness.Handler.OnHandle = async _ =>
        {
            await cancellation.CancelAsync();
            cancellation.Token.ThrowIfCancellationRequested();
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Dispatcher.ExecuteAsync(harness.Claimed, cancellation.Token));

        Assert.Equal(JobState.Pending, harness.Job.State);
        Assert.Null(harness.Job.LockedUntil);
    }

    [Fact]
    public async Task A_job_that_vanished_from_the_database_is_skipped_quietly()
    {
        var harness = new Harness(includeJob: false);

        await harness.Dispatcher.ExecuteAsync(harness.Claimed, CancellationToken.None);

        Assert.Empty(harness.Handler.Executions);
    }

    private sealed class Harness
    {
        public Harness(int maxAttempts = 3, string jobType = "probe", bool includeJob = true)
        {
            Job = new Job(Guid.CreateVersion7(), jobType, """{"n":1}""");
            Job.Start(_now, TimeSpan.FromMinutes(5));

            if (includeJob)
            {
                Jobs.Items.Add(Job);
            }

            Claimed = new ClaimedJob(Job.Id, Job.TenantId, jobType, Job.Payload, Job.Attempts);

            var options = Options.Create(new JobWorkerOptions { MaxAttempts = maxAttempts, Lease = TimeSpan.FromMinutes(5) });

            Dispatcher = new JobDispatcher(
                [Handler],
                Jobs,
                UnitOfWork,
                CurrentJob,
                options,
                new FakeClock(_now),
                NullLogger<JobDispatcher>.Instance);
        }

        public JobDispatcher Dispatcher { get; }

        public Job Job { get; }

        public ClaimedJob Claimed { get; }

        public FakeJobHandler Handler { get; } = new("probe");

        public FakeJobRepository Jobs { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; } = new();

        public FakeCurrentJob CurrentJob { get; } = new();
    }
}
