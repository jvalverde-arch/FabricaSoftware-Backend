using Microsoft.Extensions.DependencyInjection;
using SoftwareFactory.Application.Common.Jobs;
using SoftwareFactory.Application.Platform;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Persistence.Repositories;

namespace SoftwareFactory.Infrastructure.Jobs;

public static class JobServiceCollectionExtensions
{
    /// <summary>Queue services (doc 03, D6). Handlers are registered by whoever hosts the worker.</summary>
    public static IServiceCollection AddJobQueue(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IJobEnqueuer, JobEnqueuer>();
        services.AddScoped<IJobReader, JobReader>();
        services.AddScoped<IJobClaimer, JobClaimer>();
        services.AddScoped<JobDispatcher>();

        services.AddScoped<ScopedCurrentJob>();
        services.AddScoped<ICurrentJob>(provider => provider.GetRequiredService<ScopedCurrentJob>());
        services.AddScoped<ICurrentJobWriter>(provider => provider.GetRequiredService<ScopedCurrentJob>());

        return services;
    }
}
