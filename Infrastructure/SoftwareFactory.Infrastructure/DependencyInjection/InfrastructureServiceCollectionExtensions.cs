using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SoftwareFactory.Application.Common.Persistence;
using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Application.Common.Tenancy;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Persistence;
using SoftwareFactory.Infrastructure.Persistence.Initialization;
using SoftwareFactory.Infrastructure.Persistence.Options;
using SoftwareFactory.Infrastructure.Persistence.Repositories;
using SoftwareFactory.Infrastructure.Persistence.Tenancy;
using SoftwareFactory.Infrastructure.Security;
using SoftwareFactory.Infrastructure.Security.Identity;
using SoftwareFactory.Infrastructure.Security.Jwt;

namespace SoftwareFactory.Infrastructure.DependencyInjection;

/// <summary>Registration entry point consumed by the hosts' composition roots.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddOptions<SeedOptions>().Bind(configuration.GetSection(SeedOptions.SectionName));
        services.AddOptions<Argon2Options>()
            .Bind(configuration.GetSection(Argon2Options.SectionName))
            .Validate(argon2 => argon2.IsValid(), "Argon2 parameters are out of range (memory >= 8*parallelism KiB, iterations >= 1, salt >= 8, hash >= 16).")
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(jwt => jwt.IsValid(), "Jwt settings are incomplete: Issuer, Audience, ActiveKeyId and SigningKeys (unique KeyId, Secret of 32+ characters, one of them the active key) are required.")
            .ValidateOnStart();

        services.AddSingleton<DatabaseConnectionStrings>();
        services.AddScoped<ScopedTenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<ScopedTenantContext>());
        services.AddScoped<ITenantContextWriter>(provider => provider.GetRequiredService<ScopedTenantContext>());
        services.AddScoped<TenantConnectionInterceptor>();

        services.AddDbContext<SoftwareFactoryDbContext>((provider, options) =>
        {
            SoftwareFactoryDbContextOptions.Configure(options, provider.GetRequiredService<DatabaseConnectionStrings>().Application);
            options.AddInterceptors(provider.GetRequiredService<TenantConnectionInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAppUserRepository, AppUserRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();

        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddPlatformIdentity();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        return services;
    }

    /// <summary>Development only: migrate, provision the application login role and seed the local tenant at startup.</summary>
    public static IServiceCollection AddDevelopmentDatabaseInitializer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<DatabaseSeeder>();
        services.AddHostedService<DatabaseInitializer>();

        return services;
    }
}
