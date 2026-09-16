namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// Public contract of the traceability module for artifacts (HU-001). It is the only way in: neither the Api nor the
/// agents touch the tables, so validation, versioning and audit always happen.
/// </summary>
public interface IArtifactService
{
    Task<ArtifactDetailDto> CreateAsync(CreateArtifactCommand command, CancellationToken cancellationToken);

    /// <summary>Applies the change and writes a new version when the content changed (HU-001 §2).</summary>
    Task<ArtifactDetailDto> UpdateAsync(UpdateArtifactCommand command, CancellationToken cancellationToken);

    /// <summary>Content exactly as it was written, with the schema version it conforms to.</summary>
    Task<ArtifactDetailDto> GetAsync(Guid artifactId, CancellationToken cancellationToken);

    /// <summary>
    /// Content brought to the current schema of its type so an editor can render it (HU-001 schema evolution).
    /// Nothing is stored: the upgrade is persisted only if the person saves.
    /// </summary>
    Task<ArtifactDetailDto> GetForEditingAsync(Guid artifactId, CancellationToken cancellationToken);

    Task<ArtifactPage> SearchAsync(ArtifactFilter filter, CancellationToken cancellationToken);

    Task<IReadOnlyList<ArtifactVersionDto>> GetVersionsAsync(Guid artifactId, CancellationToken cancellationToken);

    Task<ArtifactDiffDto> GetDiffAsync(Guid artifactId, int fromVersion, int toVersion, CancellationToken cancellationToken);

    /// <summary>Logical delete; refused while relations point at the artifact.</summary>
    Task DeleteAsync(Guid artifactId, CancellationToken cancellationToken);

    Task<ArtifactDto> SetScoreAsync(Guid artifactId, int? score, CancellationToken cancellationToken);
}
