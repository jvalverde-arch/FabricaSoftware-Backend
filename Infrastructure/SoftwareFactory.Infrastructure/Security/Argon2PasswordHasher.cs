using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Common.Security;

namespace SoftwareFactory.Infrastructure.Security;

/// <summary>
/// Argon2id hasher producing PHC strings: <c>$argon2id$v=19$m=&lt;KiB&gt;,t=&lt;iterations&gt;,p=&lt;parallelism&gt;$&lt;salt&gt;$&lt;hash&gt;</c>
/// with salt and hash in unpadded standard Base64. Verification reads the parameters from the hash itself, so tuning the
/// configuration never invalidates stored passwords. The IPasswordHasher of T-004 must verify exactly this format.
/// </summary>
public sealed class Argon2PasswordHasher(IOptions<Argon2Options> options) : IPasswordHasher
{
    private const string Algorithm = "argon2id";
    private const int Version = 19;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var parameters = options.Value;
        var salt = RandomNumberGenerator.GetBytes(parameters.SaltLength);
        var hash = Compute(password, salt, parameters.MemoryKiB, parameters.Iterations, parameters.Parallelism, parameters.HashLength);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"${Algorithm}$v={Version}$m={parameters.MemoryKiB},t={parameters.Iterations},p={parameters.Parallelism}${Base64(salt)}${Base64(hash)}");
    }

    public bool Verify(string passwordHash, string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(passwordHash);
        ArgumentException.ThrowIfNullOrEmpty(password);

        if (!TryParse(passwordHash, out var parsed))
        {
            return false;
        }

        var computed = Compute(password, parsed.Salt, parsed.MemoryKiB, parsed.Iterations, parsed.Parallelism, parsed.Hash.Length);
        return CryptographicOperations.FixedTimeEquals(computed, parsed.Hash);
    }

    internal static bool TryParse(string passwordHash, out ParsedHash parsed)
    {
        parsed = default;

        // Expected: ["", "argon2id", "v=19", "m=...,t=...,p=...", salt, hash]
        var parts = passwordHash.Split('$');

        if (parts.Length != 6
            || parts[0].Length != 0
            || !string.Equals(parts[1], Algorithm, StringComparison.Ordinal)
            || !string.Equals(parts[2], "v=19", StringComparison.Ordinal))
        {
            return false;
        }

        int? memory = null, iterations = null, parallelism = null;

        foreach (var pair in parts[3].Split(','))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0 || !int.TryParse(pair.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            switch (pair[..separator])
            {
                case "m":
                    memory = value;
                    break;
                case "t":
                    iterations = value;
                    break;
                case "p":
                    parallelism = value;
                    break;
                default:
                    return false;
            }
        }

        if (memory is null || iterations is null || parallelism is null
            || !TryDecodeBase64(parts[4], out var salt)
            || !TryDecodeBase64(parts[5], out var hash))
        {
            return false;
        }

        parsed = new ParsedHash(memory.Value, iterations.Value, parallelism.Value, salt, hash);
        return true;
    }

    private static byte[] Compute(string password, byte[] salt, int memoryKiB, int iterations, int parallelism, int hashLength)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKiB,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };

        return argon2.GetBytes(hashLength);
    }

    private static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    private static bool TryDecodeBase64(string text, out byte[] bytes)
    {
        var padded = text.PadRight(text.Length + ((4 - (text.Length % 4)) % 4), '=');
        var buffer = new byte[padded.Length];

        if (Convert.TryFromBase64String(padded, buffer, out var written) && written > 0)
        {
            bytes = buffer[..written];
            return true;
        }

        bytes = [];
        return false;
    }

    internal readonly record struct ParsedHash(int MemoryKiB, int Iterations, int Parallelism, byte[] Salt, byte[] Hash);
}
