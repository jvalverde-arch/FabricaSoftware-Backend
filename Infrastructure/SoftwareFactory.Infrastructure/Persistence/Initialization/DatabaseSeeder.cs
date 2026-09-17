using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Domain.Traceability;
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

            await EnsureCompetenceMapAsync(context, tenantId, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gives the tenant the base competence map of HU-003 §3 if it has none. The migration seeds the tenants that
    /// already existed; a tenant born after it — today only this one — gets it here, because a tenant without a map
    /// cannot record a single decision (the log fails closed on an unmapped type).
    /// </summary>
    private static async Task EnsureCompetenceMapAsync(SoftwareFactoryDbContext context, Guid tenantId, CancellationToken cancellationToken)
    {
        // The rows are compared in memory on purpose: the role is stored through a value converter, so building the
        // comparison key in SQL would compare 'functional' against 'Functional' and seed the map twice.
        var mapped = await context.ArtifactTypeRoles
            .Select(map => new { map.ArtifactType, map.Role })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var missing = ArtifactTypeRoleSeed.Base
            .Where(pair => !mapped.Any(existing =>
                string.Equals(existing.ArtifactType, pair.ArtifactType, StringComparison.Ordinal) && existing.Role == pair.Role))
            .Select(pair => new ArtifactTypeRole(tenantId, pair.ArtifactType, pair.Role))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        context.ArtifactTypeRoles.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
