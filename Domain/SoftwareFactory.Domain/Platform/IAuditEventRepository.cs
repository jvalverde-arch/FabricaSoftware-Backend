namespace SoftwareFactory.Domain.Platform;

/// <summary>Append-only: events are added, never changed.</summary>
public interface IAuditEventRepository
{
    void Add(AuditEvent auditEvent);
}
