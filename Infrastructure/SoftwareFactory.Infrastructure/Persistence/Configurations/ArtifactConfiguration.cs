using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Project;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class ArtifactConfiguration : TenantScopedConfiguration<Artifact>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Artifact> builder)
    {
        builder.ToTable("artifact", table =>
        {
            table.HasCheckConstraint("ck_artifact_state", CheckConstraints.EnumIn<ArtifactState>("state"));
            table.HasCheckConstraint("ck_artifact_level", CheckConstraints.EnumIn<ArtifactLevel>("level"));
            table.HasCheckConstraint("ck_artifact_score", $"score IS NULL OR (score BETWEEN {Artifact.MinScore} AND {Artifact.MaxScore})");
            table.HasCheckConstraint("ck_artifact_current_version", "current_version >= 0");
        });

        builder.Property(artifact => artifact.Type).IsRequired();
        builder.Property(artifact => artifact.Title).IsRequired();
        builder.Property(artifact => artifact.State).HasConversion<SnakeCaseEnumConverter<ArtifactState>>().IsRequired();
        builder.Property(artifact => artifact.Level).HasConversion<SnakeCaseEnumConverter<ArtifactLevel>>().IsRequired();
        builder.Ignore(artifact => artifact.IsDeleted);
        builder.HasOne<SoftwareProject>().WithMany().HasForeignKey(artifact => artifact.ProjectId).OnDelete(DeleteBehavior.Restrict);

        // Lists and the matrix filter by tenant, project and type (sprint-01, HU-001).
        builder.HasIndex(artifact => new { artifact.TenantId, artifact.ProjectId, artifact.Type });
        builder.HasIndex(artifact => artifact.ProjectId);
        builder.UseXminConcurrencyToken();
    }
}
