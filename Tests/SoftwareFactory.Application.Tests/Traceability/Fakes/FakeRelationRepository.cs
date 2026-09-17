using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Application.Tests.Traceability.Fakes;

/// <summary>In-memory relations. The neighborhood walk and the orphan query belong to the database, so those two are
/// covered by the integration tests; here the fake only answers what the service reasons about.</summary>
internal sealed class FakeRelationRepository : IRelationRepository
{
    public List<Relation> Relations { get; } = [];

    public void Add(Relation relation) => Relations.Add(relation);

    public void Remove(Relation relation) => Relations.Remove(relation);

    public Task<Relation?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Relations.SingleOrDefault(relation => relation.Id == id));

    public Task<bool> ExistsAsync(Guid sourceId, Guid targetId, string type, CancellationToken cancellationToken) =>
        Task.FromResult(Relations.Any(relation =>
            relation.SourceId == sourceId && relation.TargetId == targetId && string.Equals(relation.Type, type, StringComparison.Ordinal)));

    public Task<IReadOnlyList<Relation>> GetForArtifactAsync(Guid artifactId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Relation>>(
            [.. Relations.Where(relation => relation.SourceId == artifactId || relation.TargetId == artifactId)]);

    public Task<ArtifactNeighborhood> GetNeighborhoodAsync(Guid rootId, int levels, CancellationToken cancellationToken) =>
        Task.FromResult(new ArtifactNeighborhood([], []));

    public Task<IReadOnlyList<Artifact>> GetOrphansAsync(Guid projectId, string type, string missingRelation, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Artifact>>([]);
}
