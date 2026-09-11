namespace SoftwareFactory.Application.Common.Tenancy;

/// <summary>Establishes the tenant of the current unit of work: from the token claim on authenticated requests, or from the user found during sign-in.</summary>
public interface ITenantContextWriter
{
    void Establish(Guid tenantId);
}
