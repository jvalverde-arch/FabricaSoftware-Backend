using SoftwareFactory.Application.Platform.Contracts;

namespace SoftwareFactory.Api.Security;

/// <summary>Refresh-token cookie of estandar-auth.md §1: <c>HttpOnly; Secure; SameSite=Strict; Path=/api/auth</c>.</summary>
internal static class RefreshCookie
{
    public const string Name = "sf_refresh";
    public const string CookiePath = "/api/auth";

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    public static void Write(HttpResponse response, AuthSession session) =>
        response.Cookies.Append(Name, session.RefreshToken, Options(session.RefreshTokenExpiresAt));

    public static void Delete(HttpResponse response) => response.Cookies.Delete(Name, Options(expires: null));

    private static CookieOptions Options(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expires,
        IsEssential = true,
    };
}
