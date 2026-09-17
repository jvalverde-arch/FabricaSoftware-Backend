using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class RelationConfiguration : TenantScopedConfiguration<Relation>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Relation> builder)
    {
        builder.ToTable("relation", table =>
        {
            table.HasCheckConstraint("ck_relation_created_by_type", CheckConstraints.EnumIn<AuthorType>("created_by_type"));
            table.HasCheckConstraint("ck_relation_not_reflexive", "source_id <> target_id");
        });

        builder.Property(relation => relation.Type).IsRequired();
        builder.Property(relation => relation.Metadata).HasColumnType("jsonb");
        builder.Property(relation => relation.CreatedByType).HasConversion<SnakeCaseEnumConverter<AuthorType>>().IsRequired();
        builder.HasOne<Artifact>().WithMany().HasForeignKey(relation => relation.SourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Artifact>().WithMany().HasForeignKey(relation => relation.TargetId).OnDelete(DeleteBehavior.Restrict);

        // Invariant of the model plus the two traversal directions of the neighborhood query (sprint-01, HU-002).
        builder.HasIndex(relation => new { relation.SourceId, relation.TargetId, relation.Type }).IsUnique();

        // Tenant first, then the endpoint: row-level security adds tenant_id to every predicate, so a composite index
        // answers the policy and the hop in a single scan. Measured on the graph of HU-002 (10k artifacts, 50k
        // relations): with single-column indexes the planner intersects a bitmap of the whole tenant with each
        // endpoint index and the three-level walk costs ~250 ms; with these it costs a few. A tenant-only query still
        // uses them by prefix, so the standalone tenant index is gone.
        builder.HasIndex(relation => new { relation.TenantId, relation.SourceId });
        builder.HasIndex(relation => new { relation.TenantId, relation.TargetId });
    }
}
