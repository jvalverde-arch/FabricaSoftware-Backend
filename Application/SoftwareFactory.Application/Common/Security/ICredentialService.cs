using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Common.Security;

/// <summary>Password lifecycle of a user (verification, policy, change). Implemented over ASP.NET Core Identity in Infrastructure.</summary>
public interface ICredentialService
{
    Task<bool> CheckPasswordAsync(AppUser user, string password, CancellationToken cancellationToken);

    /// <summary>Validates the new password against the policy and replaces the stored hash; the security stamp changes with it.</summary>
    Task<IReadOnlyList<PasswordChangeError>> ChangePasswordAsync(AppUser user, string currentPassword, string newPassword, CancellationToken cancellationToken);
}
