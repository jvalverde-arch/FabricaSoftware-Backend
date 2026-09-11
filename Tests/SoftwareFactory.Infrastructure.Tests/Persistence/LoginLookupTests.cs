using Microsoft.EntityFrameworkCore;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Persistence.Repositories;

namespace SoftwareFactory.Infrastructure.Tests.Persistence;

/// <summary>
/// Sign-in happens before a tenant is known. The only thing a tenant-less session may read is the rows of the one email it
/// names (policy <c>login_lookup</c> on <c>app_user</c>); everything else stays closed.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class LoginLookupTests(PostgresFixture fixture) : IAsyncLifetime
{
    private string _slugA = string.Empty;
    private Guid _tenantA;
    private Guid _tenantB;

    public async Task InitializeAsync()
    {
        await using var admin = fixture.CreateAdminContext();
        _slugA = "login-" + Guid.NewGuid().ToString("N");
        _tenantA = await TenantGraph.CreateAsync(admin, _slugA);
        _tenantB = await TenantGraph.CreateAsync(admin);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Without_a_tenant_the_repository_finds_exactly_the_users_of_the_named_email()
    {
        await using var context = fixture.CreateAppContext(tenantId: null);
        var repository = new AppUserRepository(context);

        var candidates = await repository.FindLoginCandidatesAsync(AppUser.Normalize(TenantGraph.EmailOf(_slugA)), CancellationToken.None);

        var user = Assert.Single(candidates);
        Assert.Equal(_tenantA, user.TenantId);
        Assert.Equal(TenantGraph.EmailOf(_slugA), user.Email);
    }

    [Fact]
    public async Task The_same_email_in_two_tenants_yields_both_candidates()
    {
        await using (var admin = fixture.CreateAdminContext())
        {
            admin.Users.Add(new AppUser(_tenantB, TenantGraph.EmailOf(_slugA), "Twin", TenantGraph.PasswordHash));
            await admin.SaveChangesAsync();
        }

        await using var context = fixture.CreateAppContext(tenantId: null);
        var repository = new AppUserRepository(context);

        var candidates = await repository.FindLoginCandidatesAsync(AppUser.Normalize(TenantGraph.EmailOf(_slugA)), CancellationToken.None);

        Assert.Equal(2, candidates.Count);
        Assert.Equal([_tenantA, _tenantB], candidates.Select(user => user.TenantId).Order());
    }

    [Fact]
    public async Task The_lookup_does_not_leave_the_session_open_for_other_reads()
    {
        await using var context = fixture.CreateAppContext(tenantId: null);
        var repository = new AppUserRepository(context);

        await repository.FindLoginCandidatesAsync(AppUser.Normalize(TenantGraph.EmailOf(_slugA)), CancellationToken.None);

        Assert.Equal(0, await context.Users.AsNoTracking().CountAsync());
        Assert.Equal(0, await context.Tenants.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task With_a_tenant_established_the_lookup_policy_does_not_widen_the_view()
    {
        await using var context = fixture.CreateAppContext(_tenantB);
        var repository = new AppUserRepository(context);

        var candidates = await repository.FindLoginCandidatesAsync(AppUser.Normalize(TenantGraph.EmailOf(_slugA)), CancellationToken.None);

        Assert.Empty(candidates);
    }
}
