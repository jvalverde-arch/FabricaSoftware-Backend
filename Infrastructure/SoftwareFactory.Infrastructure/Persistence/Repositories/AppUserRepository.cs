using Microsoft.EntityFrameworkCore;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Persistence.Security;

namespace SoftwareFactory.Infrastructure.Persistence.Repositories;

public sealed class AppUserRepository(SoftwareFactoryDbContext context) : IAppUserRepository
{
    /// <summary>
    /// Runs in its own transaction with the <c>app.login_email</c> setting scoped to it (<c>set_config(..., true)</c>), so the
    /// <c>login_lookup</c> policy opens exactly those rows for exactly this query and the session is closed again afterwards.
    /// </summary>
    public async Task<IReadOnlyList<AppUser>> FindLoginCandidatesAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);

        var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (transaction.ConfigureAwait(false))
        {
            await context.Database
                .ExecuteSqlAsync($"SELECT set_config({RowLevelSecurity.LoginEmailSetting}, {normalizedEmail}, true)", cancellationToken)
                .ConfigureAwait(false);

            var users = await context.Users
                .Where(user => user.NormalizedEmail == normalizedEmail)
                .OrderBy(user => user.TenantId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return users;
        }
    }

    public Task<AppUser?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Role>> GetRolesAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == userId)
            .OrderBy(userRole => userRole.Role)
            .Select(userRole => userRole.Role)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
