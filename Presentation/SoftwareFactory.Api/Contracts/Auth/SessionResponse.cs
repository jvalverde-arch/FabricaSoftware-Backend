using SoftwareFactory.Application.Platform.Contracts;

namespace SoftwareFactory.Api.Contracts.Auth;

/// <summary>Body of login and refresh. The refresh token never appears here: it travels only in the HttpOnly cookie.</summary>
public sealed record SessionResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, SessionUserResponse User)
{
    public static SessionResponse From(AuthSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new SessionResponse(session.AccessToken, session.AccessTokenExpiresAt, SessionUserResponse.From(session.User));
    }
}
