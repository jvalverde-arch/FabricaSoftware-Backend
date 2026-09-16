using Microsoft.AspNetCore.Identity;
using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Infrastructure.Security.Identity;

/// <summary>Password verification and change through Identity's <see cref="UserManager{TUser}"/>: hasher, policy validators and re-hash on demand.</summary>
internal sealed class IdentityCredentialService(UserManager<AppUser> userManager) : ICredentialService
{
    public Task<bool> CheckPasswordAsync(AppUser user, string password, CancellationToken cancellationToken) =>
        userManager.CheckPasswordAsync(user, password);

    public async Task<IReadOnlyList<PasswordChangeError>> ChangePasswordAsync(AppUser user, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword).ConfigureAwait(false);

        return result.Succeeded ? [] : [.. result.Errors.Select(Map).Distinct()];
    }

    private static PasswordChangeError Map(IdentityError error) => error.Code switch
    {
        nameof(IdentityErrorDescriber.PasswordMismatch) => PasswordChangeError.IncorrectCurrentPassword,
        nameof(IdentityErrorDescriber.PasswordTooShort) => PasswordChangeError.TooShort,
        CommonPasswordValidator.ErrorCode => PasswordChangeError.TooCommon,
        _ => throw new InvalidOperationException($"Unexpected Identity error '{error.Code}': {error.Description}"),
    };
}
