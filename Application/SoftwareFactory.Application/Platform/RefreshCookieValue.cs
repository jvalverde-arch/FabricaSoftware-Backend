using System.Globalization;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Platform;

/// <summary>
/// Value stored in the refresh cookie: <c>&lt;tenant id, 32 hex&gt;.&lt;opaque secret&gt;</c>. The tenant prefix only routes the
/// lookup (row-level security needs a session tenant before the token row can be read); the secret's hash must then match
/// a row of that very tenant, so a forged prefix yields nothing.
/// </summary>
internal static class RefreshCookieValue
{
    private const char Separator = '.';

    public static string Compose(Guid tenantId, RefreshTokenSecret secret) =>
        string.Create(CultureInfo.InvariantCulture, $"{tenantId:N}{Separator}{secret.Value}");

    public static bool TryParse(string? value, out Guid tenantId, out string secret)
    {
        tenantId = Guid.Empty;
        secret = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var separator = value.IndexOf(Separator, StringComparison.Ordinal);

        if (separator <= 0
            || separator == value.Length - 1
            || !Guid.TryParseExact(value.AsSpan(0, separator), "N", out tenantId)
            || tenantId == Guid.Empty)
        {
            return false;
        }

        secret = value[(separator + 1)..];
        return true;
    }
}
