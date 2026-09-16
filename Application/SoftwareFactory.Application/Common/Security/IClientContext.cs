using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Common.Security;

/// <summary>Where the current request comes from (IP, user agent), for the audit trail. The host reads it from the connection.</summary>
public interface IClientContext
{
    AuditClient Client { get; }
}
