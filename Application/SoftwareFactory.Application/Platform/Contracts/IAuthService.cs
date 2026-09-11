using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Platform.Contracts;

/// <summary>Sign-in, session refresh and credential changes (estandar-auth.md §1-§3). Failures that must not leak detail return null.</summary>
public interface IAuthService
{
    /// <summary>Email + password → session, or null for unknown user, wrong password, lockout or inactive account/tenant.</summary>
    Task<AuthSession?> LoginAsync(LoginCommand command, AuditClient client, CancellationToken cancellationToken);

    /// <summary>Rotates the refresh token and issues a new pair, or null when the token is unknown, expired, revoked or reused.</summary>
    Task<AuthSession?> RefreshAsync(string refreshToken, AuditClient client, CancellationToken cancellationToken);

    /// <summary>Revokes the session family the refresh token belongs to. Idempotent.</summary>
    Task LogoutAsync(string? refreshToken, AuditClient client, CancellationToken cancellationToken);

    /// <summary>Changes the password of the current user and revokes every session.</summary>
    Task<IReadOnlyList<PasswordChangeError>> ChangePasswordAsync(ChangePasswordCommand command, AuditClient client, CancellationToken cancellationToken);

    Task<SessionUser?> GetCurrentUserAsync(CancellationToken cancellationToken);
}
