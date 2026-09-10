using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Persistence.Options;
using SoftwareFactory.Infrastructure.Persistence.Security;

namespace SoftwareFactory.Infrastructure.Persistence.Initialization;

/// <summary>
/// Seeds the «local» tenant and its administrator (sprint-00, T-003). Idempotent: existing rows are left untouched.
/// The administrator's password hash is produced by <see cref="IPasswordHasher"/> in PHC format
/// (<c>$argon2id$v=19$m=...,t=...,p=...$salt$hash</c>) with the parameters of the Argon2 configuration section.
/// CONTRACT FOR T-004: the IPasswordHasher registered for ASP.NET Core Identity must verify exactly this PHC format,
/// reading the parameters from the stored hash; otherwise the seeded administrator cannot log in.
/// </summary>
public sealed class DatabaseSeeder(IPasswordHasher passwordHasher, IOptions<SeedOptions> options, ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(SoftwareFactoryDbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var seed = options.Value;
        var tenantId = SeedOptions.LocalTenantId;

        var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (transaction.ConfigureAwait(false))
        {
            // Forced RLS also applies to the owner: the session must declare the tenant it seeds.
            var tenantSetting = tenantId.ToString("D", CultureInfo.InvariantCulture);
            await context.Database
                .ExecuteSqlAsync($"SELECT set_config({RowLevelSecurity.TenantSetting}, {tenantSetting}, true)", cancellationToken)
                .ConfigureAwait(false);

            var tenant = await context.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken).ConfigureAwait(false);

            if (tenant is null)
            {
                tenant = new Tenant(tenantId, seed.TenantName, seed.TenantSlug);
                context.Tenants.Add(tenant);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                logger.TenantSeeded(seed.TenantSlug);
            }

            if (string.IsNullOrEmpty(seed.AdminPassword))
            {
                logger.AdminPasswordMissing();
            }
            else
            {
                await EnsureAdminAsync(context, tenantId, seed, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EnsureAdminAsync(SoftwareFactoryDbContext context, Guid tenantId, SeedOptions seed, CancellationToken cancellationToken)
    {
        var normalizedEmail = AppUser.Normalize(seed.AdminEmail);

        var exists = await context.Users
            .AnyAsync(user => user.TenantId == tenantId && user.NormalizedEmail == normalizedEmail, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        var admin = new AppUser(tenantId, seed.AdminEmail, seed.AdminDisplayName, passwordHasher.Hash(seed.AdminPassword!));
        context.Users.Add(admin);
        context.UserRoles.Add(new UserRole(tenantId, admin.Id, Role.Admin));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.AdminSeeded(seed.AdminEmail, seed.TenantSlug);
    }
}
