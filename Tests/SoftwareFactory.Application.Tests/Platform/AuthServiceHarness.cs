using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Platform;
using SoftwareFactory.Application.Platform.Contracts;
using SoftwareFactory.Application.Tests.Platform.Fakes;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Tests.Platform;

/// <summary>An <see cref="AuthService"/> wired to in-memory doubles, with one active tenant and one Functional user.</summary>
internal sealed class AuthServiceHarness
{
    public const string Email = "user@tenant.test";
    public const string Password = "correct-horse-battery-staple";

    public static readonly DateTimeOffset Start = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    public AuthServiceHarness()
    {
        Tenant = new Tenant("Tenant", "tenant");
        Tenants.Tenants.Add(Tenant);
        User = AddUser(Tenant.Id, Email, Password, Role.Functional);

        Service = new AuthService(
            Users,
            Tenants,
            RefreshTokens,
            Audit,
            UnitOfWork,
            new FakeCredentialService(),
            new FakeAccessTokenIssuer(Clock),
            TenantWriter,
            CurrentUser,
            new FakeClientContext(Client),
            new LoginCommandValidator(),
            new ChangePasswordCommandValidator(Options.Create(new AuthOptions())),
            Options.Create(new AuthOptions()),
            Clock,
            NullLogger<AuthService>.Instance);
    }

    public AuthService Service { get; }

    public Tenant Tenant { get; }

    public AppUser User { get; }

    public FakeAppUserRepository Users { get; } = new();

    public FakeTenantRepository Tenants { get; } = new();

    public FakeRefreshTokenRepository RefreshTokens { get; } = new();

    public FakeAuditEventRepository Audit { get; } = new();

    public FakeUnitOfWork UnitOfWork { get; } = new();

    public FakeTenantContextWriter TenantWriter { get; } = new();

    public FakeCurrentUser CurrentUser { get; } = new();

    public FakeClock Clock { get; } = new(Start);

    public AuditClient Client { get; } = new("203.0.113.7", "xunit");

    public AppUser AddUser(Guid tenantId, string email, string password, params Role[] roles)
    {
        var user = new AppUser(tenantId, email, "Test user", FakeCredentialService.HashOf(password));
        Users.Users.Add(user);
        Users.Roles[user.Id] = [.. roles];
        return user;
    }

    public Task<AuthSession?> LoginAsync(string email = Email, string password = Password) =>
        Service.LoginAsync(new LoginCommand(email, password), CancellationToken.None);
}
