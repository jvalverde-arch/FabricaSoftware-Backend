using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class DecisionAuthorRoleConfiguration : TenantScopedConfiguration<DecisionAuthorRole>
{
    protected override void ConfigureEntity(EntityTypeBuilder<DecisionAuthorRole> builder)
    {
        builder.ToTable(
            "decision_author_role",
            table => table.HasCheckConstraint("ck_decision_author_role_role", CheckConstraints.EnumIn<Role>("role")));

        builder.Property(author => author.Role).HasConversion<SnakeCaseEnumConverter<Role>>().IsRequired();
        builder.HasOne<Decision>().WithMany().HasForeignKey(author => author.DecisionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(author => new { author.DecisionId, author.Role }).IsUnique();

        // This snapshot is only ever read back with its decision — nobody searches the log by the author's hat — so
        // the tenant leads the one composite the reads need (estandar-backend.md §4).
        builder.HasIndex(author => new { author.TenantId, author.DecisionId });
    }
}
