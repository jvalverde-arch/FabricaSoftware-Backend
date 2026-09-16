using Microsoft.EntityFrameworkCore;
using SoftwareFactory.Domain.Finops;
using SoftwareFactory.Infrastructure.Persistence.Repositories;

namespace SoftwareFactory.Infrastructure.Tests.Persistence;

/// <summary>
/// Acceptance criterion of T-006: a SQL query returns what one agent run cost, and the trace stays inside its tenant.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class LlmCallRepositoryTests(PostgresFixture fixture) : IAsyncLifetime
{
    private Guid _tenantId;
    private Guid _jobId;

    /// <summary>The probe graph of the fixture already has one call for this job; assertions measure the delta.</summary>
    private decimal _baselineCost;
    private int _baselineCalls;

    public async Task InitializeAsync()
    {
        await using var admin = fixture.CreateAdminContext();
        _tenantId = await TenantGraph.CreateAsync(admin);
        _jobId = await admin.Jobs.Where(job => job.TenantId == _tenantId).Select(job => job.Id).FirstAsync();
        _baselineCost = await admin.LlmCalls.Where(call => call.JobId == _jobId).SumAsync(call => call.Cost);
        _baselineCalls = await admin.LlmCalls.CountAsync(call => call.JobId == _jobId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Records_calls_and_sums_what_a_job_has_spent()
    {
        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            var repository = new LlmCallRepository(context);
            repository.Add(new LlmCall(_tenantId, _jobId, "anthropic", "claude-sonnet-5", 1_200, 350, 800, 64, 940, 0.005900m));
            repository.Add(new LlmCall(_tenantId, _jobId, "anthropic", "claude-haiku-4-5", 5_000, 900, 0, 0, 310, 0.009500m));
            // A call of the same tenant that belongs to no job must not count towards the job.
            repository.Add(new LlmCall(_tenantId, null, "local", "qwen3-8b", 100, 50, 0, 0, 40, 0m));
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            var repository = new LlmCallRepository(context);

            Assert.Equal(_baselineCost + 0.015400m, await repository.GetJobCostAsync(_jobId, CancellationToken.None));
            Assert.Equal(0m, await repository.GetJobCostAsync(Guid.CreateVersion7(), CancellationToken.None));
        }
    }

    [Fact]
    public async Task The_cost_of_a_run_is_one_sql_query()
    {
        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            var repository = new LlmCallRepository(context);
            repository.Add(new LlmCall(_tenantId, _jobId, "anthropic", "claude-sonnet-5", 2_000, 1_000, 0, 0, 500, 0.014000m));
            repository.Add(new LlmCall(_tenantId, _jobId, "anthropic", "claude-sonnet-5", 1_000, 500, 0, 0, 400, 0.007000m));
            await context.SaveChangesAsync();
        }

        await using var query = fixture.CreateAppContext(_tenantId);

        // The report of E11: cost, calls and tokens of one run, straight from SQL.
        var summary = await query.Database
            .SqlQuery<JobCostRow>($"""
                SELECT job_id AS "JobId",
                       COUNT(*)::int AS "Calls",
                       SUM(input_tokens)::int AS "InputTokens",
                       SUM(output_tokens)::int AS "OutputTokens",
                       SUM(cost) AS "Cost"
                FROM llm_call
                WHERE job_id = {_jobId}
                GROUP BY job_id
                """)
            .SingleAsync();

        Assert.Equal(_jobId, summary.JobId);
        Assert.Equal(_baselineCalls + 2, summary.Calls);
        Assert.Equal(_baselineCost + 0.021000m, summary.Cost);
        Assert.True(summary.InputTokens >= 3_000);
        Assert.True(summary.OutputTokens >= 1_500);
    }

    [Fact]
    public async Task Another_tenant_neither_sees_the_calls_nor_their_cost()
    {
        Guid otherTenant;

        await using (var admin = fixture.CreateAdminContext())
        {
            otherTenant = await TenantGraph.CreateAsync(admin);
        }

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            new LlmCallRepository(context).Add(new LlmCall(_tenantId, _jobId, "anthropic", "claude-sonnet-5", 10, 10, 0, 0, 10, 0.000120m));
            await context.SaveChangesAsync();
        }

        await using var foreign = fixture.CreateAppContext(otherTenant);

        Assert.Equal(0m, await new LlmCallRepository(foreign).GetJobCostAsync(_jobId, CancellationToken.None));
        Assert.Equal(0, await foreign.LlmCalls.CountAsync(call => call.JobId == _jobId));
    }

    private sealed record JobCostRow(Guid JobId, int Calls, int InputTokens, int OutputTokens, decimal Cost);
}
