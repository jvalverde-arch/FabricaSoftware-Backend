namespace SoftwareFactory.Infrastructure.Security.Jwt;

/// <summary>
/// Access-token settings (estandar-auth.md §1 and §6): own issuer/audience, 15-minute lifetime, HS256 with a <c>kid</c> header and
/// a window of several listed keys so a rotation does not invalidate live sessions. Only <see cref="ActiveKeyId"/> signs.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    public string ActiveKeyId { get; set; } = string.Empty;

    public IList<JwtSigningKey> SigningKeys { get; init; } = [];

    public JwtSigningKey ActiveKey => SigningKeys.Single(key => string.Equals(key.KeyId, ActiveKeyId, StringComparison.Ordinal));

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Issuer)
        && !string.IsNullOrWhiteSpace(Audience)
        && AccessTokenLifetime > TimeSpan.Zero
        && SigningKeys.Count > 0
        && SigningKeys.All(key => key.IsValid())
        && SigningKeys.Select(key => key.KeyId).Distinct(StringComparer.Ordinal).Count() == SigningKeys.Count
        && SigningKeys.Any(key => string.Equals(key.KeyId, ActiveKeyId, StringComparison.Ordinal));
}
