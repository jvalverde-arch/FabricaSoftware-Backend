namespace SoftwareFactory.Domain.Platform;

/// <summary>Where an audited action came from. The user agent is truncated so a hostile header cannot bloat the log.</summary>
public sealed record AuditClient
{
    public const int MaxUserAgentLength = 512;

    public AuditClient(string? ipAddress, string? userAgent)
    {
        IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();
        UserAgent = string.IsNullOrWhiteSpace(userAgent)
            ? null
            : userAgent.Trim() is var trimmed && trimmed.Length > MaxUserAgentLength ? trimmed[..MaxUserAgentLength] : trimmed;
    }

    public string? IpAddress { get; }

    public string? UserAgent { get; }
}
