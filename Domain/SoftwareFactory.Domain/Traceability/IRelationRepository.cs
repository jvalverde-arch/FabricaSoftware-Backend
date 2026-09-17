namespace SoftwareFactory.Domain.Traceability;

/// <summary>
/// Relations of the current tenant (HU-002). As with artifacts, the methods carry intention and never an IQueryable
/// (estandar-backend.md §2); row-level security does the tenant filtering.
/// </summary>
public interface IRelationRepository
{
    void Add(Relation relation);

    void Remove(Relation relation);

    Task<Relation?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>True when the same source, target and type already exist: the unique index of the model.</summary>
    Task<bool> ExistsAsync(Guid sourceId, Guid targetId, string type, CancellationToken cancellationToken);

    /// <summary>Relations with the artifact at either end, newest first.</summary>
    Task<IReadOnlyList<Relation>> GetForArtifactAsync(Guid artifactId, CancellationToken cancellationToken);

    /// <summary>
    /// Everything within <paramref name="levels"/> hops of the artifact, in both directions and in one round trip
    /// (HU-002 §3). Cycles are bounded by the depth, never followed twice.
    /// </summary>
    Task<ArtifactNeighborhood> GetNeighborhoodAsync(Guid rootId, int levels, CancellationToken cancellationToken);

    /// <summary>
    /// Artifacts of <paramref name="type"/> in the project with no relation of type <paramref name="missingRelation"/>
    /// at either end (HU-002 §4): the base of the coverage matrix of S8.
    /// </summary>
    Task<IReadOnlyList<Artifact>> GetOrphansAsync(Guid projectId, string type, string missingRelation, CancellationToken cancellationToken);
}

/// <summary>An artifact reached by the neighborhood walk, with the fewest hops it took to get there.</summary>
public sealed record NeighborhoodHop(
    Guid Id,
    Guid ProjectId,
    string Type,
    string Title,
    ArtifactState State,
    ArtifactLevel Level,
    int? Score,
    int Depth);

/// <summary>A relation between two artifacts that the walk reached.</summary>
public sealed record NeighborhoodLink(Guid Id, Guid SourceId, Guid TargetId, string Type);

/// <summary>The artifacts around one artifact and the relations between them.</summary>
public sealed record ArtifactNeighborhood(IReadOnlyList<NeighborhoodHop> Nodes, IReadOnlyList<NeighborhoodLink> Links);
