using FluentValidation;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Tests.Platform;

public sealed class AuthServiceLoginTests
{
    [Fact]
    public async Task Valid_credentials_return_a_session_scoped_to_the_users_tenant()
    {
        var harness = new AuthServiceHarness();

        var session = await harness.LoginAsync();

        Assert.NotNull(session);
        Assert.Equal(harness.Tenant.Id, harness.TenantWriter.TenantId);
        Assert.Equal(harness.User.Id, session.User.Id);
        Assert.Equal(harness.Tenant.Id, session.User.TenantId);
        Assert.Equal([Role.Functional], session.User.Roles);
        Assert.StartsWith($"access:{harness.User.Id}:", session.AccessToken, StringComparison.Ordinal);
        Assert.Equal(AuthServiceHarness.Start.AddMinutes(15), session.AccessTokenExpiresAt);
        Assert.Equal(AuthServiceHarness.Start.AddDays(14), session.RefreshTokenExpiresAt);

        var stored = Assert.Single(harness.RefreshTokens.Tokens);
        Assert.Equal(harness.User.Id, stored.UserId);
        Assert.DoesNotContain(stored.TokenHash, session.RefreshToken, StringComparison.Ordinal);
        Assert.Equal(1, harness.UnitOfWork.Commits);

        var audit = Assert.Single(harness.Audit.Events);
        Assert.Equal(AuditAction.LoginSucceeded, audit.Action);
        Assert.Equal(harness.User.Id, audit.ActorId);
        Assert.Equal(AuthorType.Human, audit.ActorType);
        Assert.Equal("203.0.113.7", audit.IpAddress);
        Assert.Equal("xunit", audit.UserAgent);
    }

    [Fact]
    public async Task Email_lookup_is_case_insensitive()
    {
        var harness = new AuthServiceHarness();

        var session = await harness.LoginAsync(email: "USER@Tenant.TEST");

        Assert.NotNull(session);
    }

    [Fact]
    public async Task Wrong_password_fails_counts_the_attempt_and_is_audited()
    {
        var harness = new AuthServiceHarness();

        var session = await harness.LoginAsync(password: "wrong-password-here");

        Assert.Null(session);
        Assert.Equal(1, harness.User.FailedAccessCount);
        Assert.Empty(harness.RefreshTokens.Tokens);
        Assert.Equal(1, harness.UnitOfWork.Commits);
        var audit = Assert.Single(harness.Audit.Events);
        Assert.Equal(AuditAction.LoginFailed, audit.Action);
        Assert.Equal(harness.User.Id, audit.ActorId);
    }

    [Fact]
    public async Task Fifth_wrong_password_locks_the_account_and_audits_the_lockout()
    {
        var harness = new AuthServiceHarness();

        for (var i = 0; i < 5; i++)
        {
            Assert.Null(await harness.LoginAsync(password: "wrong-password-here"));
        }

        Assert.True(harness.User.IsLockedOut(AuthServiceHarness.Start));
        Assert.Equal(5, harness.Audit.Events.Count(e => e.Action == AuditAction.LoginFailed));
        var lockout = Assert.Single(harness.Audit.Events, e => e.Action == AuditAction.Lockout);
        Assert.Equal(harness.User.Id, lockout.ActorId);
    }

    [Fact]
    public async Task Locked_account_rejects_the_right_password_until_the_lockout_ends()
    {
        var harness = new AuthServiceHarness();
        harness.User.RecordFailedAccess(AuthServiceHarness.Start);
        var failures = harness.User.FailedAccessCount;

        for (var i = failures; i < AppUser.LockoutThreshold; i++)
        {
            harness.User.RecordFailedAccess(AuthServiceHarness.Start);
        }

        Assert.Null(await harness.LoginAsync());
        Assert.Equal(AppUser.LockoutThreshold, harness.User.FailedAccessCount);
        Assert.Equal(AuditAction.LoginFailed, Assert.Single(harness.Audit.Events).Action);

        harness.Clock.Advance(TimeSpan.FromMinutes(1));

        Assert.NotNull(await harness.LoginAsync());
        Assert.Equal(0, harness.User.FailedAccessCount);
    }

    [Fact]
    public async Task Unknown_email_fails_without_establishing_a_tenant()
    {
        var harness = new AuthServiceHarness();

        var session = await harness.LoginAsync(email: "nobody@tenant.test");

        Assert.Null(session);
        Assert.Null(harness.TenantWriter.TenantId);
        Assert.Empty(harness.Audit.Events);
        Assert.Equal(0, harness.UnitOfWork.Commits);
    }

    [Fact]
    public async Task Email_present_in_two_tenants_is_rejected()
    {
        var harness = new AuthServiceHarness();
        var other = new Tenant("Other", "other");
        harness.Tenants.Tenants.Add(other);
        harness.AddUser(other.Id, AuthServiceHarness.Email, AuthServiceHarness.Password, Role.Reader);

        var session = await harness.LoginAsync();

        Assert.Null(session);
        Assert.Null(harness.TenantWriter.TenantId);
    }

    [Fact]
    public async Task Inactive_user_cannot_sign_in()
    {
        var harness = new AuthServiceHarness();
        harness.User.Deactivate();

        Assert.Null(await harness.LoginAsync());
        Assert.Equal(AuditAction.LoginFailed, Assert.Single(harness.Audit.Events).Action);
    }

    [Fact]
    public async Task Inactive_tenant_cannot_sign_in()
    {
        var harness = new AuthServiceHarness();
        harness.Tenant.Deactivate();

        Assert.Null(await harness.LoginAsync());
        Assert.Empty(harness.RefreshTokens.Tokens);
    }

    [Theory]
    [InlineData("", "some-password-value")]
    [InlineData("not-an-email", "some-password-value")]
    [InlineData("user@tenant.test", "")]
    public async Task Invalid_command_is_rejected_by_validation(string email, string password)
    {
        var harness = new AuthServiceHarness();

        await Assert.ThrowsAsync<ValidationException>(() => harness.Service.LoginAsync(new LoginCommand(email, password), harness.Client, CancellationToken.None));
    }
}
