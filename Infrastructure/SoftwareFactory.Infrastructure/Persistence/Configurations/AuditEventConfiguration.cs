using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : TenantScopedConfiguration<AuditEvent>
{
    protected override void ConfigureEntity(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_event", table =>
        {
            table.HasCheckConstraint("ck_audit_event_action", CheckConstraints.EnumIn<AuditAction>("action"));
            table.HasCheckConstraint("ck_audit_event_actor_type", $"actor_type IS NULL OR {CheckConstraints.EnumIn<AuthorType>("actor_type")}");
        });

        builder.Property(auditEvent => auditEvent.Action).HasConversion<SnakeCaseEnumConverter<AuditAction>>().IsRequired();
        builder.Property(auditEvent => auditEvent.ActorType).HasConversion<SnakeCaseEnumConverter<AuthorType>>();
        builder.Property(auditEvent => auditEvent.Details).HasColumnType("jsonb");
        builder.HasIndex(auditEvent => new { auditEvent.TenantId, auditEvent.OccurredAt });
        builder.HasIndex(auditEvent => auditEvent.ActorId);
    }
}
