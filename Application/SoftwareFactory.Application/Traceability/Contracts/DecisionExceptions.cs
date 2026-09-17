namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>An artifact type with no competent role configured (HU-003 §2). The log fails closed: a note nobody owns
/// is worse than a refusal, and an unmapped type is a gap in the tenant's configuration, not a decision.</summary>
public sealed class ArtifactTypeWithoutCompetentRoleException : Exception
{
    public ArtifactTypeWithoutCompetentRoleException(IReadOnlyList<string> artifactTypes)
        : base($"No role is competent for the artifact type(s) {string.Join(", ", artifactTypes ?? [])}.")
    {
        ArtifactTypes = artifactTypes ?? [];
    }

    public ArtifactTypeWithoutCompetentRoleException()
    {
    }

    public ArtifactTypeWithoutCompetentRoleException(string message)
        : base(message)
    {
    }

    public ArtifactTypeWithoutCompetentRoleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public IReadOnlyList<string> ArtifactTypes { get; } = [];
}

/// <summary>The decision is not in the log of this tenant.</summary>
public sealed class DecisionNotFoundException : Exception
{
    public DecisionNotFoundException(Guid decisionId)
        : base($"Decision {decisionId} was not found.")
    {
        DecisionId = decisionId;
    }

    public DecisionNotFoundException()
    {
    }

    public DecisionNotFoundException(string message)
        : base(message)
    {
    }

    public DecisionNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Guid DecisionId { get; }
}

/// <summary>Only a note waits for somebody; anything else has already been settled (HU-003 §4).</summary>
public sealed class DecisionNotPendingException : Exception
{
    public DecisionNotPendingException(Guid decisionId, string state)
        : base($"Decision {decisionId} is {state} and no longer waits for anybody.")
    {
        DecisionId = decisionId;
        State = state;
    }

    public DecisionNotPendingException()
    {
    }

    public DecisionNotPendingException(string message)
        : base(message)
    {
    }

    public DecisionNotPendingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Guid DecisionId { get; }

    public string State { get; } = string.Empty;
}

/// <summary>The caller holds none of the roles in the note's snapshot (HU-003 §4).</summary>
public sealed class DecisionRoleNotCompetentException : Exception
{
    public DecisionRoleNotCompetentException(Guid decisionId, IReadOnlyList<string> competentRoles)
        : base($"Decision {decisionId} can only be settled by {string.Join(", ", competentRoles ?? [])}.")
    {
        DecisionId = decisionId;
        CompetentRoles = competentRoles ?? [];
    }

    public DecisionRoleNotCompetentException()
    {
    }

    public DecisionRoleNotCompetentException(string message)
        : base(message)
    {
    }

    public DecisionRoleNotCompetentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Guid DecisionId { get; }

    public IReadOnlyList<string> CompetentRoles { get; } = [];
}

/// <summary>
/// The author of the note is trying to settle it (HU-003 §4). Taking every role of the author out of the snapshot
/// makes this impossible on the day the note is born, but the permission is evaluated against the roles in force when
/// it is settled: an author who later gains the competent role would clear the first check. Nobody closes their own
/// note, and only this guard holds that over time.
/// </summary>
public sealed class SelfRatificationException : Exception
{
    public SelfRatificationException(Guid decisionId)
        : base($"The author of decision {decisionId} cannot settle it.")
    {
        DecisionId = decisionId;
    }

    public SelfRatificationException()
    {
    }

    public SelfRatificationException(string message)
        : base(message)
    {
    }

    public SelfRatificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Guid DecisionId { get; }
}
