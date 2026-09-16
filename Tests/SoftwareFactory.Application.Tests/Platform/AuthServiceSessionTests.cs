using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Tests.Platform;

public sealed class AuthServiceSessionTests
{
    [Fact]
    public async Task Refresh_rotates_the_token_and_slides_its_expiry()
    {
        var harness = new AuthServiceHarness();
        var first = await harness.LoginAsync();
        harness.Clock.Advance(TimeSpan.FromDays(3));

        var second = await harness.Service.RefreshAsync(first!.RefreshToken, CancellationToken.None);

        Assert.NotNull(second);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal(AuthServiceHarness.Start.AddDays(17), second.RefreshTokenExpiresAt);
        Assert.Equal(AuthServiceHarness.Start.AddDays(3).AddMinutes(15), second.AccessTokenExpiresAt);
        Assert.Equal(harness.Tenant.Id, harness.TenantWriter.TenantId);

        Assert.Equal(2, harness.RefreshTokens.Tokens.Count);
        var consumed = harness.RefreshTokens.Tokens[0];
        var successor = harness.RefreshTokens.Tokens[1];
        Assert.NotNull(consumed.UsedAt);
        Assert.Equal(successor.Id, consumed.ReplacedByTokenId);
        Assert.Equal(consumed.FamilyId, successor.FamilyId);
        Assert.True(successor.IsActive(harness.Clock.GetUtcNow()));
    }

    [Fact]
    public async Task Presenting_a_consumed_token_revokes_the_whole_family_and_is_audited()
    {
        var harness = new AuthServiceHarness();
        var first = await harness.LoginAsync();
        var second = await harness.Service.RefreshAsync(first!.RefreshToken, CancellationToken.None);

        var reused = await harness.Service.RefreshAsync(first.RefreshToken, CancellationToken.None);

        Assert.Null(reused);
        Assert.All(harness.RefreshTokens.Tokens, token => Assert.NotNull(token.RevokedAt));
        Assert.Null(await harness.Service.RefreshAsync(second!.RefreshToken, CancellationToken.None));
        var audit = Assert.Single(harness.Audit.Events, e => e.Action == AuditAction.RefreshReuseDetected);
        Assert.Equal(harness.User.Id, audit.ActorId);
    }

    [Fact]
    public async Task Expired_unknown_or_malformed_tokens_are_rejected()
    {
        var harness = new AuthServiceHarness();
        var session = await harness.LoginAsync();

        Assert.Null(await harness.Service.RefreshAsync("garbage", CancellationToken.None));
        Assert.Null(await harness.Service.RefreshAsync($"{harness.Tenant.Id:N}.{RefreshTokenSecret.Generate().Value}", CancellationToken.None));

        harness.Clock.Advance(TimeSpan.FromDays(14));
        Assert.Null(await harness.Service.RefreshAsync(session!.RefreshToken, CancellationToken.None));
    }

    [Fact]
    public async Task Refresh_is_rejected_when_the_user_was_deactivated()
    {
        var harness = new AuthServiceHarness();
        var session = await harness.LoginAsync();
        harness.User.Deactivate();

        Assert.Null(await harness.Service.RefreshAsync(session!.RefreshToken, CancellationToken.None));
    }

    [Fact]
    public async Task Logout_revokes_the_family_and_is_audited()
    {
        var harness = new AuthServiceHarness();
        var session = await harness.LoginAsync();

        await harness.Service.LogoutAsync(session!.RefreshToken, CancellationToken.None);

        Assert.All(harness.RefreshTokens.Tokens, token => Assert.NotNull(token.RevokedAt));
        Assert.Null(await harness.Service.RefreshAsync(session.RefreshToken, CancellationToken.None));
        var audit = Assert.Single(harness.Audit.Events, e => e.Action == AuditAction.Logout);
        Assert.Equal(harness.User.Id, audit.ActorId);
    }

    [Fact]
    public async Task Logout_without_a_token_is_a_no_op()
    {
        var harness = new AuthServiceHarness();

        await harness.Service.LogoutAsync(null, CancellationToken.None);
        await harness.Service.LogoutAsync("garbage", CancellationToken.None);

        Assert.Empty(harness.Audit.Events);
    }

    [Fact]
    public async Task Change_password_with_wrong_current_password_fails_and_keeps_sessions()
    {
        var harness = new AuthServiceHarness();
        var session = await harness.LoginAsync();
        harness.CurrentUser.SignIn(harness.User.Id, harness.Tenant.Id, Role.Functional);

        var errors = await harness.Service.ChangePasswordAsync(new ChangePasswordCommand("not-the-password", "a-brand-new-password"), CancellationToken.None);

        Assert.Equal([Common.Security.PasswordChangeError.IncorrectCurrentPassword], errors);
        Assert.NotNull(await harness.Service.RefreshAsync(session!.RefreshToken, CancellationToken.None));
    }

    [Fact]
    public async Task Change_password_revokes_every_session_and_is_audited()
    {
        var harness = new AuthServiceHarness();
        var session = await harness.LoginAsync();
        var otherDevice = await harness.LoginAsync();
        harness.CurrentUser.SignIn(harness.User.Id, harness.Tenant.Id, Role.Functional);

        var errors = await harness.Service.ChangePasswordAsync(new ChangePasswordCommand(AuthServiceHarness.Password, "a-brand-new-password"), CancellationToken.None);

        Assert.Empty(errors);
        Assert.Null(await harness.Service.RefreshAsync(session!.RefreshToken, CancellationToken.None));
        Assert.Null(await harness.Service.RefreshAsync(otherDevice!.RefreshToken, CancellationToken.None));
        Assert.Single(harness.Audit.Events, e => e.Action == AuditAction.PasswordChanged);
        Assert.NotNull(await harness.LoginAsync(password: "a-brand-new-password"));
    }

    [Fact]
    public async Task Current_user_is_read_from_the_token_identity()
    {
        var harness = new AuthServiceHarness();

        Assert.Null(await harness.Service.GetCurrentUserAsync(CancellationToken.None));

        harness.CurrentUser.SignIn(harness.User.Id, harness.Tenant.Id, Role.Functional);
        var me = await harness.Service.GetCurrentUserAsync(CancellationToken.None);

        Assert.NotNull(me);
        Assert.Equal(harness.User.Id, me.Id);
        Assert.Equal(AuthServiceHarness.Email, me.Email);
        Assert.Equal(["functional"], me.Roles);
    }
}
