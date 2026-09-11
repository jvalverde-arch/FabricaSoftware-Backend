using FluentValidation;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Platform.Contracts;

namespace SoftwareFactory.Application.Platform;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator(IOptions<AuthOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        RuleFor(command => command.CurrentPassword).NotEmpty().MaximumLength(1024);
        RuleFor(command => command.NewPassword).NotEmpty().MinimumLength(options.Value.MinimumPasswordLength).MaximumLength(1024);
    }
}
