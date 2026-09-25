using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Application.Tests.Traceability.Fakes;

/// <summary>
/// The artifact module as the card sees it. It is a stub and not the real service because what the card tests is
/// composition — which version it asks for, which schema it pairs with it — and not how an artifact is stored.
/// </summary>
internal sealed class StubArtifactService : IArtifactService
{
    public ArtifactDetailDto Current { get; set; } = null!;

    public Dictionary<int, ArtifactDetailDto> ByVersion { get; } = [];

    public List<ArtifactVersionDto> Versions { get; } = [];

    public List<int> VersionsAsked { get; } = [];

    public Task<ArtifactDetailDto> GetAsync(Guid artifactId, CancellationToken cancellationToken) => Task.FromResult(Current);

    public Task<ArtifactDetailDto> GetVersionAsync(Guid artifactId, int version, CancellationToken cancellationToken)
    {
        VersionsAsked.Add(version);
        return Task.FromResult(ByVersion[version]);
    }

    public Task<IReadOnlyList<ArtifactVersionDto>> GetVersionsAsync(Guid artifactId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ArtifactVersionDto>>([.. Versions]);

    public Task<ArtifactDetailDto> CreateAsync(CreateArtifactCommand command, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ArtifactDetailDto> UpdateAsync(UpdateArtifactCommand command, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ArtifactDetailDto> GetForEditingAsync(Guid artifactId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ArtifactPage> SearchAsync(ArtifactFilter filter, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ArtifactDiffDto> GetDiffAsync(Guid artifactId, int fromVersion, int toVersion, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task DeleteAsync(Guid artifactId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ArtifactDto> SetScoreAsync(Guid artifactId, int? score, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
