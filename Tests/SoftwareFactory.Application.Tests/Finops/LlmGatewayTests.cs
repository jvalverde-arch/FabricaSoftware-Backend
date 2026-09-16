using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Common.Llm;
using SoftwareFactory.Application.Finops;
using SoftwareFactory.Application.Finops.Contracts;
using SoftwareFactory.Application.Tests.Finops.Fakes;
using SoftwareFactory.Application.Tests.Platform.Fakes;

namespace SoftwareFactory.Application.Tests.Finops;

/// <summary>Every LLM call is measured and recorded (doc 01, E11), and the hard budgets of T-006 stop the spending.</summary>
public sealed class LlmGatewayTests
{
    private static readonly Guid _tenantId = Guid.CreateVersion7();

    [Fact]
    public async Task Routes_the_task_to_its_tier_model_and_records_the_call()
    {
        var harness = new Harness();

        var result = await harness.Gateway.CompleteAsync(
            new LlmCompletionRequest("artifact_generation", [LlmMessage.User("Escribe una historia de usuario")]) { MaxOutputTokens = 2_000 },
            CancellationToken.None);

        Assert.Equal("respuesta del modelo", result.Text);
        Assert.Equal("anthropic", result.Provider);
        Assert.Equal("claude-sonnet-5", result.Model);
        Assert.Equal(LlmTier.Large, result.Tier);

        var request = Assert.Single(harness.Anthropic.Requests);
        Assert.Equal("claude-sonnet-5", request.Model);
        Assert.Equal(2_000, request.MaxOutputTokens);

        var call = Assert.Single(harness.Calls.Calls);
        Assert.Equal(_tenantId, call.TenantId);
        Assert.Equal("anthropic", call.Provider);
        Assert.Equal("claude-sonnet-5", call.Model);
        Assert.Equal(1_000, call.InputTokens);
        Assert.Equal(500, call.OutputTokens);
        // 1000 * 2/1e6 + 500 * 10/1e6 = 0.007
        Assert.Equal(0.007000m, call.Cost);
        Assert.Equal(0.007000m, result.Cost);
        Assert.Equal(1, harness.UnitOfWork.Commits);
    }

    [Fact]
    public async Task Measures_the_latency_of_the_call()
    {
        var harness = new Harness(step: TimeSpan.FromMilliseconds(250));

        var result = await harness.Gateway.CompleteAsync(Request(), CancellationToken.None);

        Assert.Equal(250, result.LatencyMs);
        Assert.Equal(250, Assert.Single(harness.Calls.Calls).LatencyMs);
    }

    [Fact]
    public async Task Uses_the_provider_named_by_the_tier()
    {
        var harness = new Harness();

        await harness.Gateway.CompleteAsync(new LlmCompletionRequest("classification", [LlmMessage.User("clasifica esto")]), CancellationToken.None);

        Assert.Empty(harness.Anthropic.Requests);
        Assert.Single(harness.Local.Requests);
        Assert.Equal("local", Assert.Single(harness.Calls.Calls).Provider);
    }

    [Fact]
    public async Task A_tier_pointing_at_a_provider_that_is_not_registered_fails_before_spending()
    {
        var harness = new Harness(configure: options => options.Tiers[LlmTier.Large] = new LlmTierOptions { Provider = "ghost", Model = "claude-sonnet-5" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Gateway.CompleteAsync(Request(), CancellationToken.None));
        Assert.Empty(harness.Calls.Calls);
    }

    [Fact]
    public async Task Refuses_before_calling_when_the_worst_case_of_the_call_exceeds_its_budget()
    {
        var harness = new Harness(configure: options => options.Budget.MaxCostPerCall = 0.01m);

        var exception = await Assert.ThrowsAsync<LlmBudgetExceededException>(() =>
            harness.Gateway.CompleteAsync(Request() with { MaxOutputTokens = 100_000 }, CancellationToken.None));

        Assert.Equal(LlmBudgetScope.Call, exception.Scope);
        Assert.Equal(0.01m, exception.Limit);
        Assert.Empty(harness.Anthropic.Requests);
        Assert.Empty(harness.Calls.Calls);
    }

    [Fact]
    public async Task Records_the_call_before_reporting_that_it_blew_the_per_call_budget()
    {
        var harness = new Harness();
        harness.Anthropic.Usage = new LlmUsage(InputTokens: 900_000, OutputTokens: 200_000, CacheReadTokens: 0, CacheWriteTokens: 0);

        // Worst case passes the pre-flight (small MaxOutputTokens) but the answer really cost 1.8 + 2 = 3.8.
        var exception = await Assert.ThrowsAsync<LlmBudgetExceededException>(() =>
            harness.Gateway.CompleteAsync(Request() with { MaxOutputTokens = 100 }, CancellationToken.None));

        Assert.Equal(LlmBudgetScope.Call, exception.Scope);
        Assert.Equal(3.800000m, exception.Spent);
        var call = Assert.Single(harness.Calls.Calls);
        Assert.Equal(3.800000m, call.Cost);
        Assert.Equal(1, harness.UnitOfWork.Commits);
    }

    [Fact]
    public async Task Stops_a_job_whose_remaining_budget_cannot_cover_the_next_call()
    {
        // One call fits the budget (worst case ~0.0100), two do not: after spending 0.007 the next worst case does not fit.
        var harness = new Harness(configure: options => options.Budget.MaxCostPerJob = 0.015m);
        var jobId = Guid.CreateVersion7();
        await harness.Gateway.CompleteAsync(Request() with { JobId = jobId }, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<LlmBudgetExceededException>(() =>
            harness.Gateway.CompleteAsync(Request() with { JobId = jobId }, CancellationToken.None));

        Assert.Equal(LlmBudgetScope.Job, exception.Scope);
        Assert.Equal(jobId, exception.JobId);
        Assert.Equal(0.007000m, exception.Spent);
        Assert.Single(harness.Anthropic.Requests);
        Assert.Single(harness.Calls.Calls);
    }

    [Fact]
    public async Task Records_the_call_that_crossed_the_job_budget_when_the_estimate_fell_short()
    {
        var harness = new Harness(configure: options => options.Budget.MaxCostPerJob = 0.02m);
        var jobId = Guid.CreateVersion7();

        await harness.Gateway.CompleteAsync(Request() with { JobId = jobId }, CancellationToken.None);

        // Cache writes are not part of the pre-flight estimate, so this answer costs more than predicted.
        harness.Anthropic.Usage = new LlmUsage(InputTokens: 1_000, OutputTokens: 500, CacheReadTokens: 0, CacheWriteTokens: 4_000);
        var exception = await Assert.ThrowsAsync<LlmBudgetExceededException>(() =>
            harness.Gateway.CompleteAsync(Request() with { JobId = jobId }, CancellationToken.None));

        Assert.Equal(LlmBudgetScope.Job, exception.Scope);
        // 0.007 spent + (0.007 + 4000 * 2.5/1e6) = 0.024
        Assert.Equal(0.024000m, exception.Spent);
        Assert.Equal(2, harness.Calls.Calls.Count);
        Assert.All(harness.Calls.Calls, call => Assert.Equal(jobId, call.JobId));
    }

    [Fact]
    public async Task A_call_made_inside_a_job_run_carries_its_job_id_without_being_asked()
    {
        var harness = new Harness();
        var jobId = Guid.CreateVersion7();
        harness.CurrentJob.Establish(jobId);

        await harness.Gateway.CompleteAsync(Request(), CancellationToken.None);

        Assert.Equal(jobId, Assert.Single(harness.Calls.Calls).JobId);
    }

    [Fact]
    public async Task An_explicit_job_wins_over_the_run_in_context()
    {
        var harness = new Harness();
        harness.CurrentJob.Establish(Guid.CreateVersion7());
        var explicitJob = Guid.CreateVersion7();

        await harness.Gateway.CompleteAsync(Request() with { JobId = explicitJob }, CancellationToken.None);

        Assert.Equal(explicitJob, Assert.Single(harness.Calls.Calls).JobId);
    }

    [Fact]
    public async Task A_provider_failure_is_not_recorded_as_a_cost()
    {
        var harness = new Harness();
        harness.Anthropic.Throws = new HttpRequestException("503");

        await Assert.ThrowsAsync<HttpRequestException>(() => harness.Gateway.CompleteAsync(Request(), CancellationToken.None));

        Assert.Empty(harness.Calls.Calls);
        Assert.Equal(0, harness.UnitOfWork.Commits);
    }

    [Fact]
    public async Task Without_a_tenant_in_context_nothing_is_called()
    {
        var harness = new Harness(withoutTenant: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Gateway.CompleteAsync(Request(), CancellationToken.None));
        Assert.Empty(harness.Anthropic.Requests);
    }

    private static LlmCompletionRequest Request() =>
        new("artifact_generation", [LlmMessage.User("Escribe una historia de usuario")]) { MaxOutputTokens = 1_000 };

    private sealed class Harness
    {
        public Harness(bool withoutTenant = false, TimeSpan? step = null, Action<LlmOptions>? configure = null)
        {
            var options = LlmOptionsTests.Sample();
            configure?.Invoke(options);

            Gateway = new LlmGateway(
                [Anthropic, Local],
                Calls,
                UnitOfWork,
                new FakeTenantContext(withoutTenant ? null : _tenantId),
                CurrentJob,
                Options.Create(options),
                new FakeMonotonicClock(step ?? TimeSpan.FromMilliseconds(10)),
                NullLogger<LlmGateway>.Instance);
        }

        public LlmGateway Gateway { get; }

        public FakeLlmProvider Anthropic { get; } = new("anthropic");

        public FakeLlmProvider Local { get; } = new("local");

        public FakeLlmCallRepository Calls { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; } = new();

        public FakeCurrentJob CurrentJob { get; } = new();
    }
}
