
namespace SoftwareFactory.Domain.Traceability;

/// <summary>Artifacts of the current tenant (HU-001). Methods carry intention, never an IQueryable (estandar-backend.md §2).</summary>
public interface IArtifactRepository
{
    void Add(Artifact artifact);

    void AddVersion(ArtifactVersion version);

    /// <summary>Artifact with its current state, tracked so a command can advance it. Deleted ones are not returned.</summary>
    Task<Artifact?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ArtifactVersion?> GetVersionAsync(Guid artifactId, int number, CancellationToken cancellationToken);

    Task<IReadOnlyList<ArtifactVersion>> GetVersionsAsync(Guid artifactId, CancellationToken cancellationToken);

    /// <summary>Filtered page of artifacts of a project (HU-001 §5), newest first.</summary>
    Task<IReadOnlyList<Artifact>> SearchAsync(ArtifactQuery query, CancellationToken cancellationToken);

    Task<int> CountAsync(ArtifactQuery query, CancellationToken cancellationToken);

    /// <summary>Relations that still point at the artifact; a non-empty result blocks the logical delete (HU-001 §4).</summary>
    Task<IReadOnlyList<ArtifactRelationReference>> GetActiveRelationsAsync(Guid artifactId, CancellationToken cancellationToken);
}

/// <summary>Filters of the artifact list (HU-001 §5).</summary>
public sealed record ArtifactQuery(Guid ProjectId)
{
    public string? Type { get; init; }

    public ArtifactState? State { get; init; }

    /// <summary>Module the artifact belongs to, through a <c>belongs_to</c> relation.</summary>
    public Guid? ModuleId { get; init; }

    public int? MinScore { get; init; }

    public int? MaxScore { get; init; }

    /// <summary>Case-insensitive fragment of the title.</summary>
    public string? Title { get; init; }

    public int Skip { get; init; }

    public int Take { get; init; } = 50;
}

/// <summary>A relation that keeps an artifact alive, as reported when a delete is refused.</summary>
public sealed record ArtifactRelationReference(Guid RelationId, string Type, Guid OtherArtifactId, string OtherArtifactTitle, bool Incoming);
