using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class DecisionArtifactConfiguration : TenantScopedConfiguration<DecisionArtifact>
{
    protected override void ConfigureEntity(EntityTypeBuilder<DecisionArtifact> builder)
    {
        builder.ToTable("decision_artifact");

        builder.HasOne<Decision>().WithMany().HasForeignKey(link => link.DecisionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Artifact>().WithMany().HasForeignKey(link => link.ArtifactId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(link => new { link.DecisionId, link.ArtifactId }).IsUnique();

        // Tenant first, as every index of a tenant-scoped table (estandar-backend.md §4): row-level security adds
        // tenant_id to the predicate, and «decisions of this artifact» (HU-003 §5) walks the table the other way.
        builder.HasIndex(link => new { link.TenantId, link.ArtifactId });
    }
}
