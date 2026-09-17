namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// Public contract of the traceability module for relations (HU-002). Relations are what turn loose artifacts into a
/// traceability model, so the compatibility matrix, the cross-project rule and the audit trail all live behind here.
/// </summary>
public interface IRelationService
{
    Task<RelationDto> CreateAsync(CreateRelationCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a relation of <paramref name="artifactId"/> and audits it; the artifacts at both ends are untouched.
    /// The relation has to have that artifact at one of its ends, or it is not that artifact's to delete.
    /// </summary>
    Task DeleteAsync(Guid artifactId, Guid relationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RelationDto>> GetRelationsAsync(Guid artifactId, CancellationToken cancellationToken);

    /// <summary>Artifacts related to this one up to <paramref name="levels"/> hops away (1-3), in one call.</summary>
    Task<NeighborhoodDto> GetNeighborhoodAsync(Guid artifactId, int levels, CancellationToken cancellationToken);

    /// <summary>Artifacts of <paramref name="type"/> in the project with no relation of type <paramref name="missingRelation"/>.</summary>
    Task<IReadOnlyList<OrphanDto>> GetOrphansAsync(Guid projectId, string type, string missingRelation, CancellationToken cancellationToken);
}
