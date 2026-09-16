using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Infrastructure.DependencyInjection;

namespace SoftwareFactory.Api.Composition;

/// <summary>Composition root for Infrastructure. Together with Program, the only place in the Api allowed to see Infrastructure.</summary>
internal static class InfrastructureRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        services.AddInfrastructure(configuration);

        // Authorship of what this host writes: the signed-in person. The worker registers its own (agents in S4).
        services.AddScoped<IArtifactAuthorContext, ArtifactAuthorFromCurrentUser>();

        if (environment.IsDevelopment())
        {
            // Migrates, provisions the application login role and seeds the local tenant. Production migrates from the pipeline.
            services.AddDevelopmentDatabaseInitializer();
        }

        return services;
    }
}
