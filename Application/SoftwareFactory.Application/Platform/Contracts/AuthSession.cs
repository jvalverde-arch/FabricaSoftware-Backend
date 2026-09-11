namespace SoftwareFactory.Application.Platform.Contracts;

/// <summary>Result of a sign-in or refresh: the access token for memory, the opaque refresh token for the cookie, and the user.</summary>
public sealed record AuthSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    SessionUser User);
