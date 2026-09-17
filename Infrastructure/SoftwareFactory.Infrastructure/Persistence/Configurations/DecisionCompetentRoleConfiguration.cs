using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class DecisionCompetentRoleConfiguration : TenantScopedConfiguration<DecisionCompetentRole>
{
    protected override void ConfigureEntity(EntityTypeBuilder<DecisionCompetentRole> builder)
    {
        builder.ToTable(
            "decision_competent_role",
            table => table.HasCheckConstraint("ck_decision_competent_role_role", CheckConstraints.EnumIn<Role>("role")));

        builder.Property(competent => competent.Role).HasConversion<SnakeCaseEnumConverter<Role>>().IsRequired();
        builder.HasOne<Decision>().WithMany().HasForeignKey(competent => competent.DecisionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(competent => new { competent.DecisionId, competent.Role }).IsUnique();

        // «Pending notes of my role» (HU-003 §5) starts here — tenant and role — and joins decision for the state.
        // The spec sketches one index over (tenant_id, role, state), but state lives in decision and copying it here
        // would be denormalizing without a measurement to back it (estandar-backend.md §4), so the pair is this index
        // plus ix_decision_tenant_id_project_id_state on the other side.
        builder.HasIndex(competent => new { competent.TenantId, competent.Role });
    }
}
