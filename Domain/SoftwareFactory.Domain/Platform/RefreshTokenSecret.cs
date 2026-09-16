using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace SoftwareFactory.Domain.Platform;

/// <summary>
/// Opaque refresh-token secret: 256 random bits encoded as URL-safe Base64 (43 characters). The plain value travels in the
/// cookie; only <see cref="Hash"/> is persisted (SHA-256, Base64), so a database leak does not yield usable tokens.
/// </summary>
public readonly record struct RefreshTokenSecret(string Value, string Hash)
{
    public const int SizeInBytes = 32;

    public static RefreshTokenSecret Generate()
    {
        var value = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SizeInBytes));
        return new RefreshTokenSecret(value, ComputeHash(value));
    }

    public static string ComputeHash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
