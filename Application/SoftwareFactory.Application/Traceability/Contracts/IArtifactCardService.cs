namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// The artifact card (HU-005): content, relations, versions and decisions of one artifact, aggregated in a single
/// call so the page does not open with five spinners.
/// </summary>
public interface IArtifactCardService
{
    Task<ArtifactCardDto> GetAsync(ArtifactCardQuery query, CancellationToken cancellationToken);
}
