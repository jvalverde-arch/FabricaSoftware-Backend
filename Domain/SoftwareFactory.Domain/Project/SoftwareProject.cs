using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Project;

/// <summary>A development project of a tenant (doc 01, E3). Named SoftwareProject because the module namespace is Project.</summary>
public sealed class SoftwareProject : TenantScopedEntity
{
    /// <summary>
    /// Name of the index that enforces «one name per tenant». It lives next to the invariant and not inside the
    /// mapping or the service, because both of them have to say the same thing: the mapping names the index with it
    /// and the service discriminates its catch by it (estandar-backend.md §4). A literal in either place would drift
    /// from the other in silence.
    /// </summary>
    public const string UniqueNameIndex = "ux_project_tenant_id_name";

    private SoftwareProject()
    {
    }

    public SoftwareProject(Guid tenantId, string name, string? description)
        : base(tenantId)
    {
        Name = Guard.NotBlank(name);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        State = ProjectState.Active;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public ProjectState State { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Archive()
    {
        State = ProjectState.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
