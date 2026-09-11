using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Common.Persistence;
using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Application.Common.Tenancy;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Platform;

/// <summary>
/// Sign-in and session lifecycle (estandar-auth.md §1-§3). Every failure that a client could probe returns null: the host
/// answers a generic 401 whether the email is unknown, the password wrong, the account locked or inactive.
/// </summary>
public sealed class AuthService(
    IAppUserRepository users,
    ITenantRepository tenants,
    IRefreshTokenRepository refreshTokens,
    IAuditEventRepository audit,
    IUnitOfWork unitOfWork,
    ICredentialService credentials,
    IAccessTokenIssuer accessTokens,
    ITenantContextWriter tenantContext,
    ICurrentUser currentUser,
    IValidator<LoginCommand> loginValidator,
    IValidator<ChangePasswordCommand> changePasswordValidator,
    IOptions<AuthOptions> options,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthSession?> LoginAsync(LoginCommand command, AuditClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(client);
        await loginValidator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var candidates = await users.FindLoginCandidatesAsync(AppUser.Normalize(command.Email), cancellationToken).ConfigureAwait(false);

        switch (candidates.Count)
        {
            case 0:
                logger.LoginUnknownEmail(client.IpAddress);
                return null;
            case > 1:
                logger.LoginAmbiguousEmail(candidates.Count, client.IpAddress);
                return null;
            default:
                break;
        }

        var user = candidates[0];
        tenantContext.Establish(user.TenantId);

        var tenant = await tenants.GetAsync(user.TenantId, cancellationToken).ConfigureAwait(false);

        if (tenant is not { IsActive: true })
        {
            logger.LoginInactiveTenant(user.TenantId, client.IpAddress);
            return null;
        }

        var now = clock.GetUtcNow();

        if (!user.IsActive || user.IsLockedOut(now))
        {
            return await RejectLoginAsync(user, client, now, user.IsActive ? "locked_out" : "inactive", cancellationToken).ConfigureAwait(false);
        }

        if (!await credentials.CheckPasswordAsync(user, command.Password, cancellationToken).ConfigureAwait(false))
        {
            var lockoutEnd = user.RecordFailedAccess(now);

            if (lockoutEnd is { } end)
            {
                audit.Add(new AuditEvent(user.TenantId, AuditAction.Lockout, AuthorType.Human, user.Id, client, now, Details("lockoutEnd", end.ToString("O"))));
            }

            return await RejectLoginAsync(user, client, now, "wrong_password", cancellationToken).ConfigureAwait(false);
        }

        user.ResetFailedAccess();
        var secret = RefreshTokenSecret.Generate();
        var session = await IssueSessionAsync(user, RefreshToken.StartFamily(user.TenantId, user.Id, secret.Hash, now, options.Value.RefreshTokenLifetime), secret, cancellationToken)
            .ConfigureAwait(false);
        audit.Add(new AuditEvent(user.TenantId, AuditAction.LoginSucceeded, AuthorType.Human, user.Id, client, now));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return session;
    }

    public async Task<AuthSession?> RefreshAsync(string refreshToken, AuditClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        var now = clock.GetUtcNow();
        var current = await FindPresentedTokenAsync(refreshToken, cancellationToken).ConfigureAwait(false);

        if (current is null)
        {
            logger.RefreshRejected(client.IpAddress);
            return null;
        }

        // A consumed token whose family is still alive is the reuse signal of estandar-auth.md §1; a revoked one is just invalid.
        if (current is { UsedAt: not null, RevokedAt: null })
        {
            await refreshTokens.RevokeFamilyAsync(current.FamilyId, now, cancellationToken).ConfigureAwait(false);
            audit.Add(new AuditEvent(current.TenantId, AuditAction.RefreshReuseDetected, AuthorType.Human, current.UserId, client, now, Details("familyId", current.FamilyId.ToString("D"))));
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.RefreshReuseDetected(current.UserId, current.FamilyId, client.IpAddress);
            return null;
        }

        if (!current.IsActive(now))
        {
            logger.RefreshRejected(client.IpAddress);
            return null;
        }

        var user = await users.GetAsync(current.UserId, cancellationToken).ConfigureAwait(false);

        if (user is not { IsActive: true })
        {
            return null;
        }

        var secret = RefreshTokenSecret.Generate();
        var session = await IssueSessionAsync(user, current.Rotate(secret.Hash, now, options.Value.RefreshTokenLifetime), secret, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return session;
    }

    public async Task LogoutAsync(string? refreshToken, AuditClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        var current = await FindPresentedTokenAsync(refreshToken, cancellationToken).ConfigureAwait(false);

        if (current is null)
        {
            return;
        }

        var now = clock.GetUtcNow();
        await refreshTokens.RevokeFamilyAsync(current.FamilyId, now, cancellationToken).ConfigureAwait(false);
        audit.Add(new AuditEvent(current.TenantId, AuditAction.Logout, AuthorType.Human, current.UserId, client, now));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PasswordChangeError>> ChangePasswordAsync(ChangePasswordCommand command, AuditClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(client);
        await changePasswordValidator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var user = await users.GetAsync(currentUser.UserId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The authenticated user no longer exists.");

        var errors = await credentials.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword, cancellationToken).ConfigureAwait(false);

        if (errors.Count > 0)
        {
            return errors;
        }

        var now = clock.GetUtcNow();
        await refreshTokens.RevokeAllForUserAsync(user.Id, now, cancellationToken).ConfigureAwait(false);
        audit.Add(new AuditEvent(user.TenantId, AuditAction.PasswordChanged, AuthorType.Human, user.Id, client, now));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return errors;
    }

    public async Task<SessionUser?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return null;
        }

        var user = await users.GetAsync(currentUser.UserId, cancellationToken).ConfigureAwait(false);

        return user is null ? null : new SessionUser(user.Id, user.TenantId, user.Email, user.DisplayName, await users.GetRolesAsync(user.Id, cancellationToken).ConfigureAwait(false));
    }

    private static string Details(string key, string value) => $$"""{"{{key}}":"{{value}}"}""";

    private async Task<AuthSession?> RejectLoginAsync(AppUser user, AuditClient client, DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        audit.Add(new AuditEvent(user.TenantId, AuditAction.LoginFailed, AuthorType.Human, user.Id, client, now, Details("reason", reason)));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async Task<RefreshToken?> FindPresentedTokenAsync(string? cookieValue, CancellationToken cancellationToken)
    {
        if (!RefreshCookieValue.TryParse(cookieValue, out var tenantId, out var secret))
        {
            return null;
        }

        tenantContext.Establish(tenantId);
        return await refreshTokens.FindByHashAsync(RefreshTokenSecret.ComputeHash(secret), cancellationToken).ConfigureAwait(false);
    }

    private async Task<AuthSession> IssueSessionAsync(AppUser user, RefreshToken token, RefreshTokenSecret secret, CancellationToken cancellationToken)
    {
        refreshTokens.Add(token);
        var roles = await users.GetRolesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var access = accessTokens.Issue(user, roles);

        return new AuthSession(
            access.Value,
            access.ExpiresAt,
            RefreshCookieValue.Compose(user.TenantId, secret),
            token.ExpiresAt,
            new SessionUser(user.Id, user.TenantId, user.Email, user.DisplayName, roles));
    }
}
