using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Persistence.Options;

namespace SoftwareFactory.Api.Tests.Integration;

/// <summary>Acceptance of T-004 over HTTP: login, refresh rotation and reuse, logout, change-password, me, lockout.</summary>
[Collection(ApiCollectionDefinition.Name)]
public sealed class AuthEndpointsTests(ApiFixture fixture)
{
    [Fact]
    public async Task Health_is_anonymous_and_everything_else_requires_a_token()
    {
        using var client = fixture.CreateClient();

        using var health = await client.GetAsync(new Uri("/health", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        using var me = await client.GetWithTokenAsync("/api/auth/me", accessToken: null);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal("application/problem+json", me.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Bearer", me.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
        Assert.Equal("No autenticado", (await me.Content.ReadFromJsonAsync<ProblemDetails>(AuthClientExtensions.Json))!.Title);

        using var probe = await client.GetWithTokenAsync("/api/probe/any", accessToken: null);
        Assert.Equal(HttpStatusCode.Unauthorized, probe.StatusCode);
    }

    [Fact]
    public async Task Login_returns_the_access_token_and_sets_a_hardened_refresh_cookie()
    {
        using var client = fixture.CreateClient();

        using var response = await client.PostLoginAsync(ApiFixture.AdminEmail, ApiFixture.AdminPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await AuthClientExtensions.Session.FromAsync(response);
        Assert.Equal(ApiFixture.AdminEmail, session.User.Email);
        Assert.Equal(SeedOptions.LocalTenantId, session.User.TenantId);
        Assert.Equal(["admin"], session.User.Roles);
        Assert.InRange(session.AccessTokenExpiresAt - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));

        var cookie = session.SetCookieHeader;
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(session.RefreshCookie, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var me = await client.GetWithTokenAsync("/api/auth/me", session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var user = await me.Content.ReadFromJsonAsync<AuthClientExtensions.SessionUser>(AuthClientExtensions.Json);
        Assert.NotNull(user);
        Assert.Equal(session.User.Id, user.Id);
        Assert.Equal(session.User.Email, user.Email);
        Assert.Equal(session.User.Roles, user.Roles);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_both_answer_a_generic_401()
    {
        using var client = fixture.CreateClient();

        using var wrong = await client.PostLoginAsync(ApiFixture.AdminEmail, "not-the-password-2026");
        using var unknown = await client.PostLoginAsync("nobody@local", "whatever-password-2026");

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        var wrongProblem = await wrong.Content.ReadFromJsonAsync<ProblemDetails>(AuthClientExtensions.Json);
        var unknownProblem = await unknown.Content.ReadFromJsonAsync<ProblemDetails>(AuthClientExtensions.Json);
        Assert.Equal(wrongProblem!.Detail, unknownProblem!.Detail);
        Assert.Equal("Credenciales inválidas.", wrongProblem.Detail);
        Assert.Null(AuthClientExtensions.RefreshCookieOf(wrong));
    }

    [Fact]
    public async Task Malformed_login_request_is_a_422_with_field_errors()
    {
        using var client = fixture.CreateClient();

        using var response = await client.PostLoginAsync("not-an-email", "");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(AuthClientExtensions.Json);
        Assert.NotNull(problem);
        Assert.Contains("email", problem.Errors.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("password", problem.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Five_failures_lock_the_account_and_the_right_password_is_then_refused()
    {
        var user = await fixture.CreateUserAsync("lockout@local", "Lockout-User-Password-2026", Role.Reader);
        using var client = fixture.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            using var attempt = await client.PostLoginAsync(user.Email, "wrong-password-attempt");
            Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
        }

        using var locked = await client.PostLoginAsync(user.Email, "Lockout-User-Password-2026");
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);

        var audit = await fixture.AuditEventsOfAsync(user.Id);
        Assert.Equal(6, audit.Count(e => e.Action == AuditAction.LoginFailed));
        Assert.Single(audit, e => e.Action == AuditAction.Lockout);
        Assert.All(audit, e => Assert.False(string.IsNullOrEmpty(e.UserAgent) && string.IsNullOrEmpty(e.IpAddress)));
    }

    [Fact]
    public async Task Refresh_rotates_the_cookie_and_a_reused_cookie_kills_the_session()
    {
        var user = await fixture.CreateUserAsync("refresh@local", "Refresh-User-Password-2026", Role.Functional);
        using var client = fixture.CreateClient();
        var first = await client.LoginAsync(user.Email, "Refresh-User-Password-2026");

        using var rotated = await client.PostWithRefreshCookieAsync("/api/auth/refresh", first.RefreshCookie);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var second = await AuthClientExtensions.Session.FromAsync(rotated);
        Assert.NotEqual(first.RefreshCookie, second.RefreshCookie);
        Assert.NotEqual(first.AccessToken, second.AccessToken);

        using var reused = await client.PostWithRefreshCookieAsync("/api/auth/refresh", first.RefreshCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Assert.Contains("expires=Thu, 01 Jan 1970", AuthClientExtensions.RefreshCookieHeaderOf(reused) ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        using var afterReuse = await client.PostWithRefreshCookieAsync("/api/auth/refresh", second.RefreshCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);

        Assert.Single(await fixture.AuditEventsOfAsync(user.Id), e => e.Action == AuditAction.RefreshReuseDetected);
    }

    [Fact]
    public async Task Refresh_without_cookie_or_with_garbage_is_401()
    {
        using var client = fixture.CreateClient();

        using var none = await client.PostWithRefreshCookieAsync("/api/auth/refresh", refreshCookie: null);
        using var garbage = await client.PostWithRefreshCookieAsync("/api/auth/refresh", "garbage");

        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, garbage.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_session_and_clears_the_cookie()
    {
        var user = await fixture.CreateUserAsync("logout@local", "Logout-User-Password-2026", Role.Qa);
        using var client = fixture.CreateClient();
        var session = await client.LoginAsync(user.Email, "Logout-User-Password-2026");

        using var logout = await client.PostWithRefreshCookieAsync("/api/auth/logout", session.RefreshCookie, session.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains("expires=Thu, 01 Jan 1970", AuthClientExtensions.RefreshCookieHeaderOf(logout) ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        using var refresh = await client.PostWithRefreshCookieAsync("/api/auth/refresh", session.RefreshCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        using var again = await client.PostWithRefreshCookieAsync("/api/auth/logout", refreshCookie: null);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Single(await fixture.AuditEventsOfAsync(user.Id), e => e.Action == AuditAction.Logout);
    }

    [Fact]
    public async Task Change_password_validates_policy_revokes_sessions_and_accepts_the_new_password()
    {
        var user = await fixture.CreateUserAsync("change@local", "Change-User-Password-2026", Role.Architect);
        using var client = fixture.CreateClient();
        var session = await client.LoginAsync(user.Email, "Change-User-Password-2026");
        var otherDevice = await client.LoginAsync(user.Email, "Change-User-Password-2026");

        using var anonymous = await client.PostAsJsonAsync(new Uri("/api/auth/change-password", UriKind.Relative), new { currentPassword = "x", newPassword = "y" }, AuthClientExtensions.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var wrongCurrent = await PostChangeAsync(client, session.AccessToken, "not-the-current-one", "A-Brand-New-Password-2026");
        Assert.Equal(HttpStatusCode.BadRequest, wrongCurrent.StatusCode);
        Assert.Equal("La contraseña actual no es correcta.", (await wrongCurrent.Content.ReadFromJsonAsync<ProblemDetails>(AuthClientExtensions.Json))!.Detail);

        using var tooShort = await PostChangeAsync(client, session.AccessToken, "Change-User-Password-2026", "short-pass");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooShort.StatusCode);
        Assert.Contains("newPassword", (await tooShort.Content.ReadFromJsonAsync<ValidationProblemDetails>(AuthClientExtensions.Json))!.Errors.Keys, StringComparer.OrdinalIgnoreCase);

        using var tooCommon = await PostChangeAsync(client, session.AccessToken, "Change-User-Password-2026", "passwordpassword");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooCommon.StatusCode);
        var commonProblem = await tooCommon.Content.ReadFromJsonAsync<ValidationProblemDetails>(AuthClientExtensions.Json);
        Assert.Equal(["La contraseña es demasiado común."], commonProblem!.Errors["newPassword"]);

        using var changed = await PostChangeAsync(client, session.AccessToken, "Change-User-Password-2026", "A-Brand-New-Password-2026");
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        using var oldRefresh = await client.PostWithRefreshCookieAsync("/api/auth/refresh", session.RefreshCookie);
        using var otherRefresh = await client.PostWithRefreshCookieAsync("/api/auth/refresh", otherDevice.RefreshCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, otherRefresh.StatusCode);

        using var oldLogin = await client.PostLoginAsync(user.Email, "Change-User-Password-2026");
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        await client.LoginAsync(user.Email, "A-Brand-New-Password-2026");
        Assert.Single(await fixture.AuditEventsOfAsync(user.Id), e => e.Action == AuditAction.PasswordChanged);
    }

    private static async Task<HttpResponseMessage> PostChangeAsync(HttpClient client, string accessToken, string currentPassword, string newPassword)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/auth/change-password", UriKind.Relative))
        {
            Content = JsonContent.Create(new { currentPassword, newPassword }, options: AuthClientExtensions.Json),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }
}
