using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Project;

/// <summary>A development project of a tenant (doc 01, E3). Named SoftwareProject because the module namespace is Project.</summary>
public sealed class SoftwareProject : TenantScopedEntity
{
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
