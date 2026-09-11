using System.Net;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Api.Tests.Integration;

/// <summary>Policies per role (estandar-auth.md §4) and the tenant claim as the only source of the session tenant.</summary>
[Collection(ApiCollectionDefinition.Name)]
public sealed class AuthorizationTests(ApiFixture fixture)
{
    private static readonly string[] _readerRoles = ["reader"];

    public static TheoryData<Role> Roles => [.. Enum.GetValues<Role>()];

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task A_user_passes_only_the_policy_of_its_own_role(Role role)
    {
        var name = RoleNames.Of(role);
        var user = await fixture.CreateUserAsync($"role-{name}@local", $"{name}-Role-Password-2026", role);
        using var client = fixture.CreateClient();
        var session = await client.LoginAsync(user.Email, $"{name}-Role-Password-2026");
        Assert.Equal([name], session.User.Roles);

        using var any = await client.GetWithTokenAsync("/api/probe/any", session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, any.StatusCode);

        foreach (var policy in RoleNames.All)
        {
            using var response = await client.GetWithTokenAsync($"/api/probe/{policy}", session.AccessToken);
            var expected = string.Equals(policy, name, StringComparison.Ordinal) ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
            Assert.True(expected == response.StatusCode, $"{name} -> /api/probe/{policy}: expected {expected}, got {response.StatusCode}");

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            }
        }
    }

    [Fact]
    public async Task A_user_with_several_roles_passes_each_of_their_policies()
    {
        var user = await fixture.CreateUserAsync("multi@local", "Multi-Role-Password-2026", Role.Qa, Role.Compliance);
        using var client = fixture.CreateClient();
        var session = await client.LoginAsync(user.Email, "Multi-Role-Password-2026");

        Assert.Equal(["compliance", "qa"], session.User.Roles.Order());
        using var qa = await client.GetWithTokenAsync("/api/probe/qa", session.AccessToken);
        using var compliance = await client.GetWithTokenAsync("/api/probe/compliance", session.AccessToken);
        using var admin = await client.GetWithTokenAsync("/api/probe/admin", session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, qa.StatusCode);
        Assert.Equal(HttpStatusCode.OK, compliance.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
    }

    [Fact]
    public async Task Expired_or_foreign_signed_tokens_are_401()
    {
        var user = await fixture.CreateUserAsync("tokens@local", "Tokens-User-Password-2026", Role.Reader);
        using var client = fixture.CreateClient();

        var expired = Forge(user.Id, user.TenantId, ApiFixture.SigningSecret, DateTime.UtcNow.AddMinutes(-1));
        var foreign = Forge(user.Id, user.TenantId, "some-other-secret-that-nobody-configured-0123456789", DateTime.UtcNow.AddMinutes(5));

        using var expiredResponse = await client.GetWithTokenAsync("/api/auth/me", expired);
        using var foreignResponse = await client.GetWithTokenAsync("/api/auth/me", foreign);
        Assert.Equal(HttpStatusCode.Unauthorized, expiredResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, foreignResponse.StatusCode);
    }

    [Fact]
    public async Task The_tenant_of_the_session_comes_from_the_token_so_a_wrong_tenant_claim_sees_nothing()
    {
        var user = await fixture.CreateUserAsync("tenantclaim@local", "Tenant-Claim-Password-2026", Role.Reader);
        using var client = fixture.CreateClient();

        var rightTenant = Forge(user.Id, user.TenantId, ApiFixture.SigningSecret, DateTime.UtcNow.AddMinutes(5));
        var wrongTenant = Forge(user.Id, Guid.CreateVersion7(), ApiFixture.SigningSecret, DateTime.UtcNow.AddMinutes(5));

        using var ok = await client.GetWithTokenAsync("/api/auth/me", rightTenant);
        using var wrong = await client.GetWithTokenAsync("/api/auth/me", wrongTenant);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
    }

    private static string Forge(Guid userId, Guid tenantId, string secret, DateTime expires) =>
        new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = ApiFixture.Issuer,
            Audience = ApiFixture.Audience,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret)) { KeyId = ApiFixture.SigningKeyId },
                SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [AuthClaims.Subject] = userId.ToString("D"),
                [AuthClaims.TenantId] = tenantId.ToString("D"),
                [AuthClaims.Name] = "Forged",
                [AuthClaims.Roles] = _readerRoles,
            },
        });
}
