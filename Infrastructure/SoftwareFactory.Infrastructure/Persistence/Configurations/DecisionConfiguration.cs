using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Project;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class DecisionConfiguration : TenantScopedConfiguration<Decision>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Decision> builder)
    {
        builder.ToTable("decision", table =>
        {
            table.HasCheckConstraint("ck_decision_type", CheckConstraints.EnumIn<DecisionType>("type"));
            table.HasCheckConstraint("ck_decision_state", CheckConstraints.EnumIn<DecisionState>("state"));
            table.HasCheckConstraint("ck_decision_author_type", CheckConstraints.EnumIn<AuthorType>("author_type"));
        });

        builder.Property(decision => decision.Type).HasConversion<SnakeCaseEnumConverter<DecisionType>>().IsRequired();
        builder.Property(decision => decision.State).HasConversion<SnakeCaseEnumConverter<DecisionState>>().IsRequired();
        builder.Property(decision => decision.AuthorType).HasConversion<SnakeCaseEnumConverter<AuthorType>>().IsRequired();
        builder.Property(decision => decision.Justification).IsRequired();
        builder.HasOne<SoftwareProject>().WithMany().HasForeignKey(decision => decision.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Decision>().WithMany().HasForeignKey(decision => decision.ParentDecisionId).OnDelete(DeleteBehavior.Restrict);
        // The log is read by project, and «pending notes of my role» (HU-003 §5) filters the state right after the
        // project, so the state closes the composite instead of leaving the scan to the heap.
        builder.HasIndex(decision => new { decision.TenantId, decision.ProjectId, decision.State });
        builder.HasIndex(decision => decision.ProjectId);
        builder.HasIndex(decision => decision.ParentDecisionId);
        builder.UseXminConcurrencyToken();
    }
}
