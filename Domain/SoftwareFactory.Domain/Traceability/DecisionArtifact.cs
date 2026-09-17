using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Traceability;

/// <summary>One of the artifacts a <see cref="Decision"/> bears on (HU-003 §1). A decision touches at least one.</summary>
public sealed class DecisionArtifact : TenantScopedEntity
{
    private DecisionArtifact()
    {
    }

    public DecisionArtifact(Guid tenantId, Guid decisionId, Guid artifactId)
        : base(tenantId)
    {
        Guard.NotEmpty(decisionId);
        Guard.NotEmpty(artifactId);
        DecisionId = decisionId;
        ArtifactId = artifactId;
    }

    public Guid DecisionId { get; private set; }

    public Guid ArtifactId { get; private set; }
}
