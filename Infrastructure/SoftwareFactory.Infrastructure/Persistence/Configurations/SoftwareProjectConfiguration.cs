using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Project;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class SoftwareProjectConfiguration : TenantScopedConfiguration<SoftwareProject>
{
    protected override void ConfigureEntity(EntityTypeBuilder<SoftwareProject> builder)
    {
        builder.ToTable("project", table => table.HasCheckConstraint("ck_project_state", CheckConstraints.EnumIn<ProjectState>("state")));
        builder.Property(project => project.Name).IsRequired();
        builder.Property(project => project.State).HasConversion<SnakeCaseEnumConverter<ProjectState>>().IsRequired();
        builder.HasIndex(project => new { project.TenantId, project.Name })
            .IsUnique()
            .HasDatabaseName(SoftwareProject.UniqueNameIndex);
        builder.UseXminConcurrencyToken();
    }
}
