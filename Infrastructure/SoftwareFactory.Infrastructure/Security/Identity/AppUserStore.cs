using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Persistence;

namespace SoftwareFactory.Infrastructure.Security.Identity;

/// <summary>
/// ASP.NET Core Identity store over the platform's own <c>app_user</c> table (estandar-auth.md: Identity on our Postgres).
/// The email is the user name. Changes are tracked, never saved here: the unit of work commits the whole operation.
/// Users are created and removed by the administration module, not through Identity.
/// </summary>
internal sealed class AppUserStore(SoftwareFactoryDbContext context) : IUserPasswordStore<AppUser>, IUserSecurityStampStore<AppUser>
{
    public void Dispose()
    {
        // The DbContext is owned by the scope.
    }

    public Task<string> GetUserIdAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(Required(user).Id.ToString("D"));

    public Task<string?> GetUserNameAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(Required(user).Email);

    public Task SetUserNameAsync(AppUser user, string? userName, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The email of a user is changed by the administration module.");

    public Task<string?> GetNormalizedUserNameAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(Required(user).NormalizedEmail);

    public Task SetNormalizedUserNameAsync(AppUser user, string? normalizedName, CancellationToken cancellationToken) =>
        Task.CompletedTask; // Derived from the email by the entity itself.

    public Task<IdentityResult> CreateAsync(AppUser user, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Users are created by the administration module.");

    public Task<IdentityResult> UpdateAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);

    public Task<IdentityResult> DeleteAsync(AppUser user, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Users are deactivated by the administration module.");

    public Task<AppUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        Guid.TryParse(userId, out var id)
            ? context.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken)
            : Task.FromResult<AppUser?>(null);

    public Task<AppUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        context.Users.SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedUserName, cancellationToken);

    public Task SetPasswordHashAsync(AppUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        Required(user).RehashPassword(passwordHash);
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(Required(user).PasswordHash);

    public Task<bool> HasPasswordAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(true);

    public Task SetSecurityStampAsync(AppUser user, string stamp, CancellationToken cancellationToken)
    {
        Required(user).RotateSecurityStamp(); // The entity generates its own stamp; Identity's value is not needed.
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(Required(user).SecurityStamp);

    private static AppUser Required(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user;
    }
}
