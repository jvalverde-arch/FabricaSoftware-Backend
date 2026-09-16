using Microsoft.EntityFrameworkCore;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Persistence.Repositories;

namespace SoftwareFactory.Infrastructure.Tests.Persistence;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class RefreshTokenRepositoryTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromDays(14);
    private Guid _tenantId;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        await using var admin = fixture.CreateAdminContext();
        _tenantId = await TenantGraph.CreateAsync(admin);
        _userId = await admin.Users.Where(user => user.TenantId == _tenantId).Select(user => user.Id).SingleAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Finds_a_token_by_hash_within_the_tenant_and_revokes_whole_families()
    {
        var now = DateTimeOffset.UtcNow;
        var first = RefreshToken.StartFamily(_tenantId, _userId, "hash-a1", now, _lifetime);
        var second = first.Rotate("hash-a2", now, _lifetime);
        var otherFamily = RefreshToken.StartFamily(_tenantId, _userId, "hash-b1", now, _lifetime);

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            var repository = new RefreshTokenRepository(context);
            repository.Add(first);
            repository.Add(second);
            repository.Add(otherFamily);
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            var repository = new RefreshTokenRepository(context);
            var found = await repository.FindByHashAsync("hash-a2", CancellationToken.None);
            Assert.NotNull(found);
            Assert.Equal(second.Id, found.Id);
            Assert.Null(await repository.FindByHashAsync("missing", CancellationToken.None));

            await repository.RevokeFamilyAsync(first.FamilyId, now, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            var family = await context.RefreshTokens.Where(token => token.FamilyId == first.FamilyId).ToListAsync();
            Assert.Equal(2, family.Count);
            Assert.All(family, token => Assert.NotNull(token.RevokedAt));
            var untouched = await context.RefreshTokens.SingleAsync(token => token.Id == otherFamily.Id);
            Assert.Null(untouched.RevokedAt);

            await new RefreshTokenRepository(context).RevokeAllForUserAsync(_userId, now, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            Assert.All(await context.RefreshTokens.Where(token => token.UserId == _userId).ToListAsync(), token => Assert.NotNull(token.RevokedAt));
        }
    }

    [Fact]
    public async Task Another_tenant_cannot_see_the_token_even_with_its_hash()
    {
        Guid otherTenant;

        await using (var admin = fixture.CreateAdminContext())
        {
            otherTenant = await TenantGraph.CreateAsync(admin);
        }

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            new RefreshTokenRepository(context).Add(RefreshToken.StartFamily(_tenantId, _userId, "hash-secret", DateTimeOffset.UtcNow, _lifetime));
            await context.SaveChangesAsync();
        }

        await using var foreign = fixture.CreateAppContext(otherTenant);
        Assert.Null(await new RefreshTokenRepository(foreign).FindByHashAsync("hash-secret", CancellationToken.None));
    }
}
