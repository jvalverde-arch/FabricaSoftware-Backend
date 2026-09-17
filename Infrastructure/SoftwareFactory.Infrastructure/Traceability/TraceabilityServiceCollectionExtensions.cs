using Microsoft.Extensions.DependencyInjection;
using SoftwareFactory.Application.Platform;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Application.Traceability.Contracts;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence.Repositories;

namespace SoftwareFactory.Infrastructure.Traceability;

public static class TraceabilityServiceCollectionExtensions
{
    /// <summary>
    /// Traceability module (HU-001). The schema registry and the upgraders are singletons: they are the shipped
    /// catalog, identical for every request. Whoever hosts this registers the author context of its own callers.
    /// </summary>
    public static IServiceCollection AddTraceability(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(ArtifactSchemaRegistry.Embedded);
        services.AddSingleton(RelationCompatibilityMatrix.Embedded);
        services.AddSingleton<IJsonSchemaValidator, NJsonSchemaValidator>();
        services.AddSingleton(provider => new ArtifactContentMigrator(
            provider.GetRequiredService<ArtifactSchemaRegistry>(),
            provider.GetServices<IArtifactContentUpgrader>()));

        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<IArtifactService, ArtifactService>();
        services.AddScoped<IRelationRepository, RelationRepository>();
        services.AddScoped<IRelationService, RelationService>();
        services.AddScoped<IDecisionRepository, DecisionRepository>();
        services.AddScoped<IDecisionService, DecisionService>();
        services.AddScoped<IProjectTreeRepository, ProjectTreeRepository>();
        services.AddScoped<IProjectTreeService, ProjectTreeService>();

        return services;
    }
}
