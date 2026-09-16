using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SoftwareFactory.AgentRuntime.Composition;

namespace SoftwareFactory.AgentRuntime.Tests.Composition;

/// <summary>
/// The worker's object graph, checked the way the host checks it at startup. HU-001 added a service that only the Api
/// registered and the worker stopped booting; nothing failed until a container ran. This is that alarm, in CI.
/// </summary>
public sealed class WorkerCompositionTests
{
    [Fact]
    public void Every_service_the_worker_registers_can_be_constructed()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(Configuration());
        services.AddLogging();
        services.AddWorkerServices(Configuration());

        // Exactly what WebApplication.CreateBuilder does in Development: resolve the dependencies of every descriptor
        // without instantiating them, so a missing registration is a red test and not a crashed deployment.
        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        provider.Dispose();
    }

    /// <summary>Shape-only configuration: the graph is validated, never connected, so no database has to exist.</summary>
    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:softwarefactory-admin"] = "Host=nowhere;Database=softwarefactory;Username=postgres;Password=unused",
                ["Database:AppRolePassword"] = "unused-in-a-graph-that-never-connects",
                ["Jwt:ActiveKeyId"] = "test",
                ["Jwt:SigningKeys:0:KeyId"] = "test",
                ["Jwt:SigningKeys:0:Secret"] = "0123456789abcdef0123456789abcdef0123456789abcdef",
            })
            .Build();
}
