using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Infrastructure.Persistence.Initialization;
using SoftwareFactory.Infrastructure.Persistence.Options;
using SoftwareFactory.Infrastructure.Security;

namespace SoftwareFactory.Infrastructure.Tests.Persistence;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class DatabaseSeederTests(PostgresFixture fixture)
{
    private const string AdminPassword = "Local-Admin-Password-2026";

    [Fact]
    public async Task Seed_creates_the_local_tenant_and_its_admin_with_a_phc_argon2id_hash_and_is_idempotent()
    {
        var hasher = new Argon2PasswordHasher(Options.Create(new Argon2Options { MemoryKiB = 8192, Iterations = 2, Parallelism = 1 }));
        var seedOptions = Options.Create(new SeedOptions { AdminPassword = AdminPassword });
        var seeder = new DatabaseSeeder(hasher, seedOptions, NullLogger<DatabaseSeeder>.Instance);

        await using (var owner = fixture.CreateAdminContext())
        {
            await seeder.SeedAsync(owner, CancellationToken.None);
        }

        await using (var owner = fixture.CreateAdminContext())
        {
            await seeder.SeedAsync(owner, CancellationToken.None);
        }

        await using var context = fixture.CreateAppContext(SeedOptions.LocalTenantId);

        var tenant = await context.Tenants.SingleAsync();
        Assert.Equal(SeedOptions.LocalTenantId, tenant.Id);
        Assert.Equal("local", tenant.Slug);

        var admin = await context.Users.SingleAsync();
        Assert.Equal("admin@local", admin.Email);
        Assert.StartsWith("$argon2id$v=19$m=8192,t=2,p=1$", admin.PasswordHash, StringComparison.Ordinal);
        Assert.True(hasher.Verify(admin.PasswordHash, AdminPassword));

        var role = await context.UserRoles.SingleAsync();
        Assert.Equal(admin.Id, role.UserId);
        Assert.Equal(Role.Admin, role.Role);
    }
}
