using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class ArtifactVersionConfiguration : TenantScopedConfiguration<ArtifactVersion>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ArtifactVersion> builder)
    {
        builder.ToTable("artifact_version", table =>
        {
            table.HasCheckConstraint("ck_artifact_version_author_type", CheckConstraints.EnumIn<AuthorType>("author_type"));
            table.HasCheckConstraint("ck_artifact_version_number", "number >= 1");
        });

        builder.Property(version => version.Content).HasColumnType("jsonb").IsRequired();
        builder.Property(version => version.AuthorType).HasConversion<SnakeCaseEnumConverter<AuthorType>>().IsRequired();
        builder.HasOne<Artifact>().WithMany().HasForeignKey(version => version.ArtifactId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(version => new { version.ArtifactId, version.Number }).IsUnique();
        builder.HasIndex(version => version.Content).HasMethod("gin");
        builder.HasIndex(version => version.TenantId);
    }
}
