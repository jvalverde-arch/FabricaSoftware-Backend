namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// Records a decision (HU-003 §1). Neither the type nor the roles travel here: the type is derived from the
/// competence map and the author's roles come from the token or the agent's identity.
/// </summary>
public sealed record RecordDecisionCommand(Guid ProjectId, IReadOnlyList<Guid> ArtifactIds, string Justification);

/// <summary>Closes a pending note by ratifying or reverting it (HU-003 §4).</summary>
public sealed record CloseDecisionCommand(Guid DecisionId, string Justification);

/// <summary>Filters of the decision list (HU-003 §5) as callers express them, with roles by their wire names.</summary>
public sealed record DecisionFilter(Guid ProjectId)
{
    public Guid? ArtifactId { get; init; }

    /// <summary>Only pending notes this role may settle; it reads the stored snapshot, never the current map.</summary>
    public string? PendingRole { get; init; }

    public int Skip { get; init; }

    public int Take { get; init; } = 50;
}

/// <summary>An entry of the log as the rest of the platform sees it.</summary>
public sealed record DecisionDto(
    Guid Id,
    Guid ProjectId,
    string Type,
    string State,
    ArtifactAuthor Author,
    IReadOnlyList<string> AuthorRoles,
    IReadOnlyList<string> CompetentRoles,
    IReadOnlyList<Guid> ArtifactIds,
    string Justification,
    Guid? ParentDecisionId,
    DateTimeOffset CreatedAt);

public sealed record DecisionPage(IReadOnlyList<DecisionDto> Items, int Total, int Skip, int Take);

/// <summary>One entry of the competence map in force (HU-003 §2); the artifact card shows it as «who can ratify».</summary>
public sealed record ArtifactTypeRoleDto(string ArtifactType, IReadOnlyList<string> Roles);
