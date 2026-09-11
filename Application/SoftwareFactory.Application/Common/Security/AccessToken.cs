namespace SoftwareFactory.Application.Common.Security;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
