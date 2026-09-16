using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.AgentRuntime.Composition;

/// <summary>
/// Origin of what this host writes to the audit trail. There is no request behind a job run, so there is no remote
/// address either: the honest answer is the worker itself, not a blank or a borrowed IP.
/// </summary>
internal sealed class WorkerClientContext : IClientContext
{
    public AuditClient Client { get; } = new(ipAddress: null, userAgent: "SoftwareFactory.AgentRuntime");
}
