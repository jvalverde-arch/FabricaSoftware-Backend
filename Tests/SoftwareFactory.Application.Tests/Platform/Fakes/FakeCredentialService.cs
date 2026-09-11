using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Tests.Platform.Fakes;

/// <summary>Stores passwords as <c>hash:&lt;password&gt;</c>; enforces only the minimum length.</summary>
internal sealed class FakeCredentialService : ICredentialService
{
    public static string HashOf(string password) => "hash:" + password;

    public Task<bool> CheckPasswordAsync(AppUser user, string password, CancellationToken cancellationToken) =>
        Task.FromResult(user.PasswordHash == HashOf(password));

    public Task<IReadOnlyList<PasswordChangeError>> ChangePasswordAsync(AppUser user, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        if (user.PasswordHash != HashOf(currentPassword))
        {
            return Task.FromResult<IReadOnlyList<PasswordChangeError>>([PasswordChangeError.IncorrectCurrentPassword]);
        }

        if (newPassword.Length < 12)
        {
            return Task.FromResult<IReadOnlyList<PasswordChangeError>>([PasswordChangeError.TooShort]);
        }

        user.ChangePasswordHash(HashOf(newPassword));
        return Task.FromResult<IReadOnlyList<PasswordChangeError>>([]);
    }
}
