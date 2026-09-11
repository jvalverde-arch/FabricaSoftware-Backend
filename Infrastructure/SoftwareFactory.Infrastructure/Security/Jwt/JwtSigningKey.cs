namespace SoftwareFactory.Infrastructure.Security.Jwt;

/// <summary>One symmetric signing key. The secret comes from vault or configuration, never from the repository.</summary>
public sealed class JwtSigningKey
{
    public const int MinimumSecretLength = 32;

    /// <summary>Goes into the <c>kid</c> header so validators pick the right key during rotation.</summary>
    public string KeyId { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;

    public bool IsValid() => !string.IsNullOrWhiteSpace(KeyId) && Secret.Length >= MinimumSecretLength;
}
