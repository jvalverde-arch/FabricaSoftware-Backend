using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Common.Llm;
using SoftwareFactory.Application.Finops;
using SoftwareFactory.Application.Finops.Contracts;
using SoftwareFactory.Infrastructure.Jobs;
using SoftwareFactory.Infrastructure.Llm;
using SoftwareFactory.Infrastructure.Persistence;
using SoftwareFactory.Infrastructure.Persistence.Repositories;
using SoftwareFactory.Infrastructure.Tests.Persistence;
using Xunit.Abstractions;

namespace SoftwareFactory.Infrastructure.Tests.Llm;

/// <summary>
/// Full acceptance run of T-006 against a real endpoint and a real database: one call through the gateway, the row it
/// writes in <c>llm_call</c>, and the SQL that reports what the run cost. Same variables as
/// <see cref="RealEndpointLlmProviderTests"/>; excluded from the default run.
/// </summary>
[Trait("Category", "RealEndpoint")]
[Collection(PostgresCollectionDefinition.Name)]
public sealed class RealEndpointGatewayTests(PostgresFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task A_real_call_lands_in_llm_call_and_the_sql_reports_what_the_run_cost()
    {
        var model = Environment.GetEnvironmentVariable("SF_LLM_MODEL")
            ?? throw new InvalidOperationException("SF_LLM_MODEL is required (see RealEndpointLlmProviderTests).");
        var baseUrl = Environment.GetEnvironmentVariable("SF_LLM_BASE_URL")
            ?? throw new InvalidOperationException("SF_LLM_BASE_URL is required for the OpenAI-compatible endpoint.");

        Guid tenantId;
        Guid jobId;

        await using (var admin = fixture.CreateAdminContext())
        {
            tenantId = await TenantGraph.CreateAsync(admin);
            jobId = await admin.Jobs.Where(job => job.TenantId == tenantId).Select(job => job.Id).FirstAsync();
        }

        var options = Options.Create(Configuration(model, baseUrl));
        var provider = new OpenAiCompatibleLlmProvider("local", options.Value.Providers["local"], new HttpClient());

        decimal cost;

        await using (var context = fixture.CreateAppContext(tenantId))
        {
            var gateway = new LlmGateway(
                [provider],
                new LlmCallRepository(context),
                new UnitOfWork(context),
                new FixedTenantContext(tenantId),
                new ScopedCurrentJob(),
                options,
                new StopwatchMonotonicClock(),
                NullLogger<LlmGateway>.Instance);

            var result = await gateway.CompleteAsync(
                new LlmCompletionRequest("classification", [LlmMessage.User("Clasifica en una palabra: «la aplicación no carga».")])
                {
                    System = "Responde con una sola palabra.",
                    MaxOutputTokens = 32,
                    JobId = jobId,
                },
                CancellationToken.None);

            cost = result.Cost;
            Assert.False(string.IsNullOrWhiteSpace(result.Text));
            Assert.Equal("local", result.Provider);
            Assert.Equal(LlmTier.Small, result.Tier);
            Assert.True(result.Usage.InputTokens > 0);
            Assert.True(result.LatencyMs > 0);

            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"gateway → {result.Provider}/{result.Model}: {result.Usage.InputTokens} in / {result.Usage.OutputTokens} out, {result.LatencyMs} ms, cost {result.Cost}"));
        }

        await using var query = fixture.CreateAppContext(tenantId);

        var recorded = await query.LlmCalls.SingleAsync(call => call.JobId == jobId && call.Provider == "local");
        Assert.Equal(model, recorded.Model);
        Assert.Equal(cost, recorded.Cost);
        Assert.True(recorded.LatencyMs > 0);

        var runCost = await query.Database
            .SqlQuery<decimal>($"SELECT SUM(cost) AS \"Value\" FROM llm_call WHERE job_id = {jobId}")
            .SingleAsync();

        Assert.True(runCost >= cost);
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"SQL cost of the run: {runCost}"));
    }

    private static LlmOptions Configuration(string model, string baseUrl)
    {
        var options = new LlmOptions { DefaultTier = LlmTier.Small };
        options.Tasks["classification"] = LlmTier.Small;
        options.Tiers[LlmTier.Small] = new LlmTierOptions { Provider = "local", Model = model, MaxOutputTokens = 64 };
        options.Models[model] = new LlmModelPricing { InputPerMillion = 1m, OutputPerMillion = 5m };
        options.Providers["local"] = new LlmProviderOptions
        {
            Kind = LlmProviderKind.OpenAiCompatible,
            BaseUrl = new Uri(baseUrl),
            Timeout = TimeSpan.FromMinutes(2),
        };

        return options;
    }
}
