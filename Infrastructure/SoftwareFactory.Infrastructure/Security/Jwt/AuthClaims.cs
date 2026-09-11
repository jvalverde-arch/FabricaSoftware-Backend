namespace SoftwareFactory.Infrastructure.Security.Jwt;

/// <summary>Claim names of the platform access token (estandar-auth.md §1).</summary>
public static class AuthClaims
{
    public const string Subject = "sub";
    public const string TenantId = "tenant_id";
    public const string Roles = "roles";
    public const string Name = "name";
}
