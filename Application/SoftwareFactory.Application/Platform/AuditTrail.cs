using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Platform;

/// <summary>Writes what other modules report into <c>audit_event</c>, with the client of the current request.</summary>
public sealed class AuditTrail(IAuditEventRepository events, IClientContext clientContext) : IAuditTrail
{
    public void Record(Guid tenantId, AuditedAction action, AuthorType actorType, Guid actorId, DateTimeOffset occurredAt, string? details = null) =>
        events.Add(new AuditEvent(tenantId, Map(action), actorType, actorId, clientContext.Client, occurredAt, details));

    private static AuditAction Map(AuditedAction action) => action switch
    {
        AuditedAction.ArtifactCreated => AuditAction.ArtifactCreated,
        AuditedAction.ArtifactUpdated => AuditAction.ArtifactUpdated,
        AuditedAction.ArtifactStateChanged => AuditAction.ArtifactStateChanged,
        AuditedAction.ArtifactDeleted => AuditAction.ArtifactDeleted,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown audited action."),
    };
}
