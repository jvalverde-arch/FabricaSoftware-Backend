namespace SoftwareFactory.Application.Common.Tenancy;

/// <summary>
/// Tenant of the current unit of work. The hosts fill it from the authenticated token (never from the request body);
/// the persistence layer pushes it to the database session so row-level security applies.
/// </summary>
public interface ITenantContext
{
    /// <summary>Current tenant, or null when no tenant is established (the database then returns no tenant-scoped rows).</summary>
    Guid? TenantId { get; }
}
