namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>A relation that keeps an artifact alive, as the caller sees it.</summary>
public sealed record BlockingRelation(Guid RelationId, string Type, Guid OtherArtifactId, string OtherArtifactTitle, string Direction);

/// <summary>
/// The artifact cannot be deleted because relations still point at it (HU-001 §4). The list travels with the error
/// so the person sees what to undo first instead of guessing.
/// </summary>
public sealed class ArtifactHasRelationsException : Exception
{
    public ArtifactHasRelationsException(Guid artifactId, IReadOnlyList<BlockingRelation> relations)
        : base($"Artifact {artifactId} still has {relations?.Count ?? 0} active relation(s).")
    {
        ArgumentNullException.ThrowIfNull(relations);

        ArtifactId = artifactId;
        Relations = relations;
    }

    public ArtifactHasRelationsException()
    {
    }

    public ArtifactHasRelationsException(string message)
        : base(message)
    {
    }

    public ArtifactHasRelationsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Guid ArtifactId { get; }

    public IReadOnlyList<BlockingRelation> Relations { get; } = [];
}
