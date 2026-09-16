namespace SoftwareFactory.Application.Common.Security;

/// <summary>Claim names of the platform access token (estandar-auth.md §1).</summary>
public static class AuthClaims
{
    public const string Subject = "sub";
    public const string TenantId = "tenant_id";
    public const string Roles = "roles";
    public const string Name = "name";

    /// <summary>Unique token id, so two tokens issued in the same second for the same user still differ.</summary>
    public const string TokenId = "jti";
}
