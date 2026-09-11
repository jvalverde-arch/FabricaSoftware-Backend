using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : TenantScopedConfiguration<RefreshToken>
{
    protected override void ConfigureEntity(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_token");
        builder.Property(token => token.TokenHash).IsRequired();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(token => new { token.TenantId, token.TokenHash }).IsUnique();
        builder.HasIndex(token => token.FamilyId);
        builder.HasIndex(token => token.UserId);
    }
}
