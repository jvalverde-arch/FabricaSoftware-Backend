using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Application.Platform.Contracts;

/// <summary>
/// Audit trail of the platform, exposed to the other modules (doc 03 §5). It is the only way in: a module records
/// what it did without reaching into the Platform tables, which is what keeps the modules apart.
/// </summary>
public interface IAuditTrail
{
    /// <summary>Records an action of the current tenant. The event is committed with the unit of work of the caller.</summary>
    void Record(Guid tenantId, AuditedAction action, AuthorType actorType, Guid actorId, DateTimeOffset occurredAt, string? details = null);
}

/// <summary>Actions the platform audits. The Platform module maps these to its own stored values.</summary>
public enum AuditedAction
{
    ArtifactCreated,
    ArtifactUpdated,
    ArtifactStateChanged,
    ArtifactDeleted,
    RelationCreated,
    RelationDeleted,
    DecisionRecorded,
    DecisionRatified,
    DecisionReverted,
}
