using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Common.Llm;
using SoftwareFactory.Application.Finops;
using SoftwareFactory.Application.Finops.Contracts;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Jobs;
using SoftwareFactory.Infrastructure.Llm;
using SoftwareFactory.Infrastructure.Persistence;
using SoftwareFactory.Infrastructure.Persistence.Repositories;
using SoftwareFactory.Infrastructure.Tests.Persistence;

namespace SoftwareFactory.Infrastructure.Tests.Jobs;

/// <summary>
/// The trace of consumption per run depends on this: everything an agent spends inside a job lands in
/// <c>llm_call</c> with that job's id, without the caller having to remember (doc 01, E11).
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class JobCostTraceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task What_a_run_spends_is_attributed_to_the_job_that_spent_it()
    {
        Guid tenantId;
        Guid jobId;

        await using (var admin = fixture.CreateAdminContext())
        {
            tenantId = await TenantGraph.CreateAsync(admin);
            var job = new Job(tenantId, "probe", """{"steps":1}""");
            admin.Jobs.Add(job);
            await admin.SaveChangesAsync();
            jobId = job.Id;
        }

        await using (var context = fixture.CreateAppContext(tenantId))
        {
            // The worker establishes the run; nothing else in the call chain mentions the job.
            var currentJob = new ScopedCurrentJob();
            currentJob.Establish(jobId);

            var gateway = new LlmGateway(
                [new StubProvider()],
                new LlmCallRepository(context),
                new UnitOfWork(context),
                new FixedTenantContext(tenantId),
                currentJob,
                Options.Create(Configuration()),
                new StopwatchMonotonicClock(),
                NullLogger<LlmGateway>.Instance);

            await gateway.CompleteAsync(new LlmCompletionRequest("classification", [LlmMessage.User("hola")]), CancellationToken.None);
            await gateway.CompleteAsync(new LlmCompletionRequest("classification", [LlmMessage.User("otra vez")]), CancellationToken.None);
        }

        await using var query = fixture.CreateAppContext(tenantId);
        var calls = await query.LlmCalls.Where(call => call.JobId == jobId).ToListAsync();

        Assert.Equal(2, calls.Count);
        Assert.All(calls, call => Assert.Equal(jobId, call.JobId));
        Assert.Equal(0.006000m, calls.Sum(call => call.Cost));
    }

    private static LlmOptions Configuration()
    {
        var options = new LlmOptions { DefaultTier = LlmTier.Small };
        options.Tasks["classification"] = LlmTier.Small;
        options.Tiers[LlmTier.Small] = new LlmTierOptions { Provider = "stub", Model = "stub-small", MaxOutputTokens = 256 };
        options.Models["stub-small"] = new LlmModelPricing { InputPerMillion = 1m, OutputPerMillion = 5m };
        options.Providers["stub"] = new LlmProviderOptions();
        return options;
    }

    private sealed class StubProvider : ILlmProvider
    {
        public string Name => "stub";

        public Task<LlmProviderResponse> CompleteAsync(LlmProviderRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new LlmProviderResponse(
                "listo",
                request.Model,
                new LlmUsage(InputTokens: 1_000, OutputTokens: 400, CacheReadTokens: 0, CacheWriteTokens: 0),
                "end_turn"));
    }
}
