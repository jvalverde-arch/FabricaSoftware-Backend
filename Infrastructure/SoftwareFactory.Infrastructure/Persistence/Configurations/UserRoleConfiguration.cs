using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class UserRoleConfiguration : TenantScopedConfiguration<UserRole>
{
    protected override void ConfigureEntity(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_role", table => table.HasCheckConstraint("ck_user_role_role", CheckConstraints.EnumIn<Role>("role")));
        builder.Property(userRole => userRole.Role).HasConversion<SnakeCaseEnumConverter<Role>>().IsRequired();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(userRole => userRole.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(userRole => new { userRole.UserId, userRole.Role }).IsUnique();
        builder.HasIndex(userRole => userRole.TenantId);
    }
}
