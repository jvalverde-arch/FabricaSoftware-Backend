using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

/// <summary>Shared mapping of tenant-scoped tables: app-generated uuid key and a restricted foreign key to the tenant.</summary>
internal abstract class TenantScopedConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : TenantScopedEntity
{
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.TenantId).IsRequired();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);

        ConfigureEntity(builder);
    }

    protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);
}
