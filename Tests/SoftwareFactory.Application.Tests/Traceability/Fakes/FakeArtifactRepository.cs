using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Application.Tests.Traceability.Fakes;

internal sealed class FakeArtifactRepository : IArtifactRepository
{
    public List<Artifact> Artifacts { get; } = [];

    public List<ArtifactVersion> Versions { get; } = [];

    public List<ArtifactRelationReference> Relations { get; } = [];

    public void Add(Artifact artifact) => Artifacts.Add(artifact);

    public void AddVersion(ArtifactVersion version) => Versions.Add(version);

    public Task<Artifact?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Artifacts.SingleOrDefault(artifact => artifact.Id == id && !artifact.IsDeleted));

    public Task<ArtifactVersion?> GetVersionAsync(Guid artifactId, int number, CancellationToken cancellationToken) =>
        Task.FromResult(Versions.SingleOrDefault(version => version.ArtifactId == artifactId && version.Number == number));

    public Task<IReadOnlyList<ArtifactVersion>> GetVersionsAsync(Guid artifactId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ArtifactVersion>>(
            [.. Versions.Where(version => version.ArtifactId == artifactId).OrderBy(version => version.Number)]);

    public Task<IReadOnlyList<Artifact>> SearchAsync(ArtifactQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Artifact>>([.. Filter(query).Skip(query.Skip).Take(query.Take)]);

    public Task<int> CountAsync(ArtifactQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Filter(query).Count());

    public Task<IReadOnlyList<ArtifactRelationReference>> GetActiveRelationsAsync(Guid artifactId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ArtifactRelationReference>>([.. Relations]);

    private IEnumerable<Artifact> Filter(ArtifactQuery query) =>
        Artifacts
            .Where(artifact => !artifact.IsDeleted && artifact.ProjectId == query.ProjectId)
            .Where(artifact => query.Type is null || artifact.Type == query.Type)
            .Where(artifact => query.State is null || artifact.State == query.State)
            .Where(artifact => query.Title is null || artifact.Title.Contains(query.Title, StringComparison.OrdinalIgnoreCase))
            .Where(artifact => query.MinScore is null || artifact.Score >= query.MinScore)
            .Where(artifact => query.MaxScore is null || artifact.Score <= query.MaxScore);
}
