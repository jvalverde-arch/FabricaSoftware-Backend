using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class AppUserConfiguration : TenantScopedConfiguration<AppUser>
{
    protected override void ConfigureEntity(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_user");
        builder.Property(user => user.Email).IsRequired();
        builder.Property(user => user.NormalizedEmail).IsRequired();
        builder.Property(user => user.DisplayName).IsRequired();
        builder.Property(user => user.PasswordHash).IsRequired();
        builder.Property(user => user.SecurityStamp).IsRequired();
        builder.HasIndex(user => new { user.TenantId, user.NormalizedEmail }).IsUnique();
        builder.UseXminConcurrencyToken();
    }
}
