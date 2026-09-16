using Microsoft.AspNetCore.Identity;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Infrastructure.Security.Identity;

/// <summary>Rejects passwords found in the embedded common-password list (estandar-auth.md §2), case-insensitively.</summary>
internal sealed class CommonPasswordValidator : IPasswordValidator<AppUser>
{
    public const string ErrorCode = "PasswordTooCommon";

    public Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user, string? password) =>
        Task.FromResult(password is not null && CommonPasswords.Contains(password)
            ? IdentityResult.Failed(new IdentityError { Code = ErrorCode, Description = "The password is in the list of commonly used passwords." })
            : IdentityResult.Success);
}
