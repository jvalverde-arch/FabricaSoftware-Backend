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
        builder.HasIndex(relation => relation.SourceId);
        builder.HasIndex(relation => relation.TargetId);
        builder.HasIndex(relation => relation.TenantId);
    }
}
