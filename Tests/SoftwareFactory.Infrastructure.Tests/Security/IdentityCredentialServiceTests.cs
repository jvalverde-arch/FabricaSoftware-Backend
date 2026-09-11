using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Infrastructure.Persistence;
using SoftwareFactory.Infrastructure.Security;
using SoftwareFactory.Infrastructure.Security.Identity;
using SoftwareFactory.Infrastructure.Tests.Persistence;

namespace SoftwareFactory.Infrastructure.Tests.Security;

/// <summary>ASP.NET Core Identity over the platform's own tables: Argon2id PHC hashes, 12-character minimum, common-password list.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class IdentityCredentialServiceTests(PostgresFixture fixture) : IAsyncLifetime
{
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
    public async Task Verifies_the_seeded_phc_hash_and_rejects_a_wrong_password()
    {
        await using var scope = CreateScope();
        var (service, user) = await ResolveAsync(scope);

        Assert.True(await service.CheckPasswordAsync(user, TenantGraph.Password, CancellationToken.None));
        Assert.False(await service.CheckPasswordAsync(user, "definitely-not-it-2026", CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_a_wrong_current_password_on_change()
    {
        await using var scope = CreateScope();
        var (service, user) = await ResolveAsync(scope);

        var errors = await service.ChangePasswordAsync(user, "wrong-current-password", "A-Perfectly-Fine-New-Password", CancellationToken.None);

        Assert.Equal([PasswordChangeError.IncorrectCurrentPassword], errors);
    }

    [Theory]
    [InlineData("short-pass1", PasswordChangeError.TooShort)]
    [InlineData("passwordpassword", PasswordChangeError.TooCommon)]
    [InlineData("PasswordPassword", PasswordChangeError.TooCommon)]
    public async Task Enforces_the_password_policy(string newPassword, PasswordChangeError expected)
    {
        await using var scope = CreateScope();
        var (service, user) = await ResolveAsync(scope);

        var errors = await service.ChangePasswordAsync(user, TenantGraph.Password, newPassword, CancellationToken.None);

        Assert.Equal([expected], errors);
    }

    [Fact]
    public async Task Changing_the_password_stores_a_new_argon2id_hash_and_rotates_the_security_stamp()
    {
        await using var scope = CreateScope();
        var (service, user) = await ResolveAsync(scope);
        var previousStamp = user.SecurityStamp;

        var errors = await service.ChangePasswordAsync(user, TenantGraph.Password, "A-Perfectly-Fine-New-Password", CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<SoftwareFactoryDbContext>().SaveChangesAsync();

        Assert.Empty(errors);
        Assert.NotEqual(previousStamp, user.SecurityStamp);

        await using var verify = fixture.CreateAppContext(_tenantId);
        var reloaded = await verify.Users.SingleAsync(u => u.Id == _userId);
        Assert.StartsWith("$argon2id$v=19$m=8192,t=2,p=1$", reloaded.PasswordHash, StringComparison.Ordinal);
        Assert.True(new Argon2PasswordHasher(Options.Create(Argon2)).Verify(reloaded.PasswordHash, "A-Perfectly-Fine-New-Password"));
    }

    private static Argon2Options Argon2 => new() { MemoryKiB = 8192, Iterations = 2, Parallelism = 1 };

    private AsyncServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddLogging();
        services.AddScoped(_ => fixture.CreateAppContext(_tenantId));
        services.AddSingleton<IPasswordHasher>(new Argon2PasswordHasher(Options.Create(Argon2)));
        services.AddOptions<AuthOptions>();
        services.AddPlatformIdentity();
        return services.BuildServiceProvider().CreateAsyncScope();
    }

    private async Task<(ICredentialService Service, AppUser User)> ResolveAsync(AsyncServiceScope scope)
    {
        var context = scope.ServiceProvider.GetRequiredService<SoftwareFactoryDbContext>();
        var user = await context.Users.SingleAsync(u => u.Id == _userId);
        return (scope.ServiceProvider.GetRequiredService<ICredentialService>(), user);
    }
}
