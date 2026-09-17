namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// Public contract of the decision log (HU-003). It is the only way in, for people and for agents alike: what an
/// entry is, and who may close it, are computed here and never taken from the caller.
/// </summary>
public interface IDecisionService
{
    /// <summary>
    /// Records a decision or, when the artifacts touched answer to a role the author does not hold, a note pending
    /// that role's ratification (HU-003 §2).
    /// </summary>
    Task<DecisionDto> RecordAsync(RecordDecisionCommand command, CancellationToken cancellationToken);

    /// <summary>Ratifies a pending note; the caller needs a competent role and must not be the author.</summary>
    Task<DecisionDto> RatifyAsync(CloseDecisionCommand command, CancellationToken cancellationToken);

    /// <summary>Reverts a pending note; same two conditions as ratifying.</summary>
    Task<DecisionDto> RevertAsync(CloseDecisionCommand command, CancellationToken cancellationToken);

    Task<DecisionPage> SearchAsync(DecisionFilter filter, CancellationToken cancellationToken);

    /// <summary>The competence map in force for the tenant, artifact type by artifact type.</summary>
    Task<IReadOnlyList<ArtifactTypeRoleDto>> GetCompetenceMapAsync(CancellationToken cancellationToken);
}
