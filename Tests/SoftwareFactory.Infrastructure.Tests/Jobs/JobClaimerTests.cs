using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Jobs;
using SoftwareFactory.Infrastructure.Persistence;
using SoftwareFactory.Infrastructure.Tests.Persistence;

namespace SoftwareFactory.Infrastructure.Tests.Jobs;

/// <summary>
/// The claim of T-007: <c>FOR UPDATE SKIP LOCKED</c> across tenants without breaking row-level security, backoff
/// respected, and a run whose worker died taken over once its lease expires.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class JobClaimerTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset _now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private Guid _tenantA;
    private Guid _tenantB;

    public async Task InitializeAsync()
    {
        await using var admin = fixture.CreateAdminContext();
        _tenantA = await TenantGraph.CreateAsync(admin);
        _tenantB = await TenantGraph.CreateAsync(admin);

        // The probe graph queues one job per tenant; park them so each test starts from a known queue.
        await admin.Jobs.ExecuteUpdateAsync(setters => setters.SetProperty(job => job.AvailableAt, _now.AddDays(10)));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Claims_a_pending_job_of_any_tenant_and_marks_it_running()
    {
        var jobId = await QueueAsync(_tenantB, "probe", _now.AddMinutes(-1));

        var claimed = await ClaimAsync();

        Assert.NotNull(claimed);
        Assert.Equal(jobId, claimed.JobId);
        Assert.Equal(_tenantB, claimed.TenantId);
        Assert.Equal("probe", claimed.Type);
        Assert.Equal(1, claimed.Attempt);

        await using var admin = fixture.CreateAdminContext();
        var job = await admin.Jobs.SingleAsync(entity => entity.Id == jobId);
        Assert.Equal(JobState.Running, job.State);
        Assert.Equal(_now.AddMinutes(5), job.LockedUntil);
    }

    [Fact]
    public async Task An_empty_queue_returns_nothing() => Assert.Null(await ClaimAsync());

    [Fact]
    public async Task A_job_scheduled_for_later_is_not_claimed_yet()
    {
        await QueueAsync(_tenantA, "probe", _now.AddMinutes(30));

        Assert.Null(await ClaimAsync());
        Assert.NotNull(await ClaimAsync(at: _now.AddMinutes(31)));
    }

    [Fact]
    public async Task Two_workers_racing_for_one_job_get_one_each_at_most()
    {
        var first = await QueueAsync(_tenantA, "probe", _now.AddMinutes(-1));
        var second = await QueueAsync(_tenantB, "probe", _now.AddMinutes(-1));

        var claims = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ClaimAsync()));

        var taken = claims.Where(claim => claim is not null).Select(claim => claim!.JobId).ToList();
        Assert.Equal(2, taken.Count);
        Assert.Equal([first, second], taken.Order());
    }

    [Fact]
    public async Task A_run_whose_lease_expired_is_taken_over_and_keeps_counting_attempts()
    {
        var jobId = await QueueAsync(_tenantA, "probe", _now.AddMinutes(-1));
        var first = await ClaimAsync();
        Assert.NotNull(first);

        // Still leased: nobody else may take it.
        Assert.Null(await ClaimAsync(at: _now.AddMinutes(4)));

        var second = await ClaimAsync(at: _now.AddMinutes(6));

        Assert.NotNull(second);
        Assert.Equal(jobId, second.JobId);
        Assert.Equal(2, second.Attempt);
    }

    [Fact]
    public async Task The_claim_does_not_open_the_tenant_of_the_job_to_the_rest_of_the_session()
    {
        await QueueAsync(_tenantA, "probe", _now.AddMinutes(-1));

        await using var context = fixture.CreateAppContext(tenantId: null);
        var claimer = Create(context, _now);

        var claimed = await claimer.ClaimNextAsync(CancellationToken.None);

        Assert.NotNull(claimed);
        // The tenant was declared only inside the claim transaction; the session is tenant-less again.
        Assert.Equal(0, await context.Jobs.CountAsync());
        Assert.Equal(0, await context.Tenants.CountAsync());
    }

    private async Task<Guid> QueueAsync(Guid tenantId, string type, DateTimeOffset availableAt)
    {
        await using var admin = fixture.CreateAdminContext();
        var job = new Job(tenantId, type, """{"probe":true}""");
        admin.Jobs.Add(job);
        await admin.SaveChangesAsync();

        await admin.Jobs.Where(entity => entity.Id == job.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entity => entity.AvailableAt, availableAt));

        return job.Id;
    }

    private async Task<ClaimedJob?> ClaimAsync(DateTimeOffset? at = null)
    {
        await using var context = fixture.CreateAppContext(tenantId: null);
        return await Create(context, at ?? _now).ClaimNextAsync(CancellationToken.None);
    }

    private static JobClaimer Create(SoftwareFactoryDbContext context, DateTimeOffset now) =>
        new(
            context,
            Options.Create(new JobWorkerOptions { Lease = TimeSpan.FromMinutes(5) }),
            new FixedTimeProvider(now),
            NullLogger<JobClaimer>.Instance);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
