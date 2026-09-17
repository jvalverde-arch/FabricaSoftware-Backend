using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Traceability;

/// <summary>
/// Configuration of which role is competent for an artifact type, per tenant (HU-003 §2-3). Seeded with the base map
/// and editable afterwards; a type with no row here is a configuration gap, and the decision log fails closed on it
/// rather than leaving a note without an owner.
/// </summary>
public sealed class ArtifactTypeRole : TenantScopedEntity
{
    private ArtifactTypeRole()
    {
    }

    public ArtifactTypeRole(Guid tenantId, string artifactType, Role role)
        : base(tenantId)
    {
        ArtifactType = Guard.NotBlank(artifactType);
        Role = role;
    }

    /// <summary>Artifact type code from the catalog (user_story, adr, test_case, ...).</summary>
    public string ArtifactType { get; private set; } = string.Empty;

    public Role Role { get; private set; }
}
