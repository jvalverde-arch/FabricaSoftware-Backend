using SoftwareFactory.AgentRuntime.Jobs;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Application.Platform.Handlers;
using SoftwareFactory.Infrastructure.DependencyInjection;

namespace SoftwareFactory.AgentRuntime.Composition;

/// <summary>Composition root of the worker host: with Program, the only place allowed to see Infrastructure.</summary>
internal static class WorkerRegistration
{
    public static IServiceCollection AddWorkerServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddInfrastructure(configuration);
        services.AddSingleton(TimeProvider.System);

        // Handlers of sprint 0; the real agents arrive with S2.
        services.AddScoped<IJobHandler, ProbeJobHandler>();

        services.AddHostedService<JobWorker>();

        return services;
    }
}
