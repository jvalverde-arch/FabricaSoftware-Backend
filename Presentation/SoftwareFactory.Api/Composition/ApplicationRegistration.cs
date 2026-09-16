using System.Globalization;
using FluentValidation;
using SoftwareFactory.Application.Platform;
using SoftwareFactory.Application.Platform.Contracts;

namespace SoftwareFactory.Api.Composition;

/// <summary>Application services, validators and options. FluentValidation speaks Spanish to the user (CLAUDE.md rule 4).</summary>
internal static class ApplicationRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("es");

        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .Validate(auth => auth.IsValid(), "Auth settings are out of range (MinimumPasswordLength >= 12, positive RefreshTokenLifetime, Lockout.Threshold >= 1, 0 < Lockout.BaseDuration <= Lockout.MaximumDuration).")
            .ValidateOnStart();
        services.AddScoped<IValidator<LoginCommand>, LoginCommandValidator>();
        services.AddScoped<IValidator<ChangePasswordCommand>, ChangePasswordCommandValidator>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
