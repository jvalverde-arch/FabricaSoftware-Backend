using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class ArtifactTypeRoleConfiguration : TenantScopedConfiguration<ArtifactTypeRole>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ArtifactTypeRole> builder)
    {
        builder.ToTable(
            "artifact_type_role",
            table => table.HasCheckConstraint("ck_artifact_type_role_role", CheckConstraints.EnumIn<Role>("role")));

        builder.Property(map => map.ArtifactType).IsRequired();
        builder.Property(map => map.Role).HasConversion<SnakeCaseEnumConverter<Role>>().IsRequired();

        // A type may have more than one competent role (non_functional_requirement has two), but never twice the same.
        builder.HasIndex(map => new { map.TenantId, map.ArtifactType, map.Role }).IsUnique();
    }
}
