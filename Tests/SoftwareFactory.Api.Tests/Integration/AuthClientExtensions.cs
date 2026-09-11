using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SoftwareFactory.Api.Tests.Integration;

/// <summary>Talks to /api/auth the way the SPA does: JSON in, access token in memory, refresh cookie handled explicitly.</summary>
internal static class AuthClientExtensions
{
    public const string RefreshCookieName = "sf_refresh";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<HttpResponseMessage> PostLoginAsync(this HttpClient client, string email, string password) =>
        await client.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email, password }, Json);

    public static async Task<Session> LoginAsync(this HttpClient client, string email, string password)
    {
        using var response = await client.PostLoginAsync(email, password);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await Session.FromAsync(response);
    }

    public static async Task<HttpResponseMessage> PostWithRefreshCookieAsync(this HttpClient client, string path, string? refreshCookie, string? accessToken = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative));

        if (refreshCookie is not null)
        {
            request.Headers.Add("Cookie", $"{RefreshCookieName}={refreshCookie}");
        }

        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> GetWithTokenAsync(this HttpClient client, string path, string? accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));

        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request);
    }

    /// <summary>Value of the refresh cookie set by the response, or null when the response deletes/omits it.</summary>
    public static string? RefreshCookieOf(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        var header = cookies.SingleOrDefault(cookie => cookie.StartsWith(RefreshCookieName + "=", StringComparison.Ordinal));
        var value = header?.Split(';')[0][(RefreshCookieName.Length + 1)..];
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public static string? RefreshCookieHeaderOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.SingleOrDefault(cookie => cookie.StartsWith(RefreshCookieName + "=", StringComparison.Ordinal))
            : null;

    public sealed record Session(string AccessToken, DateTimeOffset AccessTokenExpiresAt, SessionUser User, string RefreshCookie, string SetCookieHeader)
    {
        public static async Task<Session> FromAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadFromJsonAsync<SessionBody>(Json);
            Assert.NotNull(body);
            var cookie = RefreshCookieOf(response);
            Assert.NotNull(cookie);
            return new Session(body.AccessToken, body.AccessTokenExpiresAt, body.User, cookie, RefreshCookieHeaderOf(response)!);
        }
    }

    public sealed record SessionBody(string AccessToken, DateTimeOffset AccessTokenExpiresAt, SessionUser User);

    public sealed record SessionUser(Guid Id, Guid TenantId, string Email, string DisplayName, string[] Roles);
}
