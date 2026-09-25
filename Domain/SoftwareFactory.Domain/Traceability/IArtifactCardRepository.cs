namespace SoftwareFactory.Domain.Traceability;

/// <summary>
/// The relation panel of the artifact card (HU-005 §2), read in one go. It is its own query and not a loop over
/// the relation repository because what the panel needs about the other end — its project, its module — is a join,
/// and asking it per relation would turn a card into a hundred round trips.
/// </summary>
public interface IArtifactCardRepository
{
    Task<IReadOnlyList<CardRelationRow>> GetRelationsAsync(Guid artifactId, CancellationToken cancellationToken);
}

/// <summary>
/// One relation as the card shows it: the other end with what is needed to draw it and to say whether following it
/// leaves the module or the project the person is standing in. <c>Upstream</c> is true when this artifact is the
/// source of the relation: it points away from it, towards what it depends on.
/// </summary>
public sealed record CardRelationRow(
    Guid RelationId,
    string RelationType,
    bool Upstream,
    Guid OtherId,
    string OtherType,
    string OtherTitle,
    ArtifactState OtherState,
    ArtifactLevel OtherLevel,
    int? OtherScore,
    Guid OtherProjectId,
    string OtherProjectName,
    Guid? OtherModuleId,
    string? OtherModuleTitle);
