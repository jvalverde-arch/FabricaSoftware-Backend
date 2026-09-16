using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Infrastructure.Persistence.Repositories;

public sealed class AuditEventRepository(SoftwareFactoryDbContext context) : IAuditEventRepository
{
    public void Add(AuditEvent auditEvent) => context.AuditEvents.Add(auditEvent);
}
