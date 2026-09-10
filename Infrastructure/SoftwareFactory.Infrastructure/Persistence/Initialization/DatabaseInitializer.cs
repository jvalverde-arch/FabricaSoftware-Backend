using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoftwareFactory.Infrastructure.Persistence.Options;

namespace SoftwareFactory.Infrastructure.Persistence.Initialization;

/// <summary>
/// Development-only startup step: migrates with the owner connection, provisions the application login role and seeds.
/// Runs before the host starts serving requests. Production applies migrations from the pipeline instead.
/// </summary>
public sealed class DatabaseInitializer(
    DatabaseConnectionStrings connectionStrings,
    DatabaseSeeder seeder,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (connectionStrings.Admin is null)
        {
            logger.AdminConnectionMissing();
            return;
        }

        var context = new SoftwareFactoryDbContext(SoftwareFactoryDbContextOptions.Create(connectionStrings.Admin));

        await using (context.ConfigureAwait(false))
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            logger.DatabaseMigrated();

            await AppRoleProvisioner
                .EnsureLoginRoleAsync(context, connectionStrings.AppRoleName, connectionStrings.AppRolePassword, cancellationToken)
                .ConfigureAwait(false);
            logger.AppRoleProvisioned(connectionStrings.AppRoleName);

            await seeder.SeedAsync(context, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
