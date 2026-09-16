using FluentValidation;
using SoftwareFactory.Application.Platform.Contracts;

namespace SoftwareFactory.Application.Platform;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(command => command.Password).NotEmpty().MaximumLength(1024);
    }
}
