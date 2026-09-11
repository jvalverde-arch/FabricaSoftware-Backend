using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Tests.Platform.Fakes;

internal sealed class FakeAuditEventRepository : IAuditEventRepository
{
    public List<AuditEvent> Events { get; } = [];

    public void Add(AuditEvent auditEvent) => Events.Add(auditEvent);
}
