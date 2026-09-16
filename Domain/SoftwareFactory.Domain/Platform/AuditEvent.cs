using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Platform;

/// <summary>
/// Append-only audit record of an action by a human or an agent (doc 03 §5, estandar-auth.md §6). Never updated or deleted:
/// the application role only holds INSERT and SELECT on its table.
/// </summary>
public sealed class AuditEvent : TenantScopedEntity
{
    private AuditEvent()
    {
    }

    public AuditEvent(
        Guid tenantId,
        AuditAction action,
        AuthorType? actorType,
        Guid? actorId,
        AuditClient client,
        DateTimeOffset occurredAt,
        string? details = null)
        : base(tenantId)
    {
        ArgumentNullException.ThrowIfNull(client);

        Action = action;
        ActorType = actorType;
        ActorId = actorId;
        IpAddress = client.IpAddress;
        UserAgent = client.UserAgent;
        OccurredAt = occurredAt.ToUniversalTime();
        Details = details is null ? null : Guard.ValidJson(details);
    }

    public AuditAction Action { get; private set; }

    public AuthorType? ActorType { get; private set; }

    public Guid? ActorId { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Optional JSON document with action-specific data (never credentials or tokens).</summary>
    public string? Details { get; private set; }
}
