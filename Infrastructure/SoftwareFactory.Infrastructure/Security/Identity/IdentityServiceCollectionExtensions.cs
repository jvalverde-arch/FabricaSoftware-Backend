using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Infrastructure.Security.Identity;

public static class IdentityServiceCollectionExtensions
{
    /// <summary>
    /// Identity Core over the platform's own store: Argon2id hashing, minimum length from <see cref="AuthOptions"/>, the
    /// common-password list, and no arbitrary complexity rules. Lockout is a domain rule, so Identity's is disabled.
    /// Requires <see cref="IPasswordHasher"/>, <see cref="AuthOptions"/>, the DbContext and logging to be registered.
    /// </summary>
    public static IServiceCollection AddPlatformIdentity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IPasswordHasher<AppUser>, Argon2IdentityPasswordHasher>();

        services.AddIdentityCore<AppUser>(identity =>
            {
                identity.Password.RequireDigit = false;
                identity.Password.RequireLowercase = false;
                identity.Password.RequireUppercase = false;
                identity.Password.RequireNonAlphanumeric = false;
                identity.Password.RequiredUniqueChars = 1;
                identity.Lockout.AllowedForNewUsers = false;
                identity.User.AllowedUserNameCharacters = string.Empty;
                identity.User.RequireUniqueEmail = false;
            })
            .AddUserStore<AppUserStore>()
            .AddPasswordValidator<CommonPasswordValidator>();

        services.AddOptions<IdentityOptions>()
            .Configure<IOptions<AuthOptions>>((identity, auth) => identity.Password.RequiredLength = auth.Value.MinimumPasswordLength);

        services.AddScoped<ICredentialService, IdentityCredentialService>();

        return services;
    }
}
