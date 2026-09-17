using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Api.Contracts.Decisions;

/// <summary>
/// Records a decision (HU-003 §1). Neither the type nor any role travels in the body: the type is derived from the
/// competence map and the roles come from the token, so nobody can name their own ratifier.
/// </summary>
public sealed record RecordDecisionRequest(IReadOnlyList<Guid> ArtifactIds, string Justification);

/// <summary>Ratifies or reverts a pending note (HU-003 §4).</summary>
public sealed record CloseDecisionRequest(string Justification);

public sealed record DecisionAuthorResponse(string Type, Guid Id);

public sealed record DecisionResponse(
    Guid Id,
    Guid ProjectId,
    string Type,
    string State,
    DecisionAuthorResponse Author,
    IReadOnlyList<string> AuthorRoles,
    IReadOnlyList<string> CompetentRoles,
    IReadOnlyList<Guid> ArtifactIds,
    string Justification,
    Guid? ParentDecisionId,
    DateTimeOffset CreatedAt);

public sealed record DecisionPageResponse(IReadOnlyList<DecisionResponse> Items, int Total, int Skip, int Take);

/// <summary>One artifact type and the roles competent for it (HU-003 §2); the artifact card shows «who can ratify».</summary>
public sealed record ArtifactTypeRoleResponse(string ArtifactType, IReadOnlyList<string> Roles);

/// <summary>Turns the decision contracts of the module into the wire shape.</summary>
public static class DecisionMapping
{
    public static DecisionResponse ToResponse(this DecisionDto decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return new DecisionResponse(
            decision.Id,
            decision.ProjectId,
            decision.Type,
            decision.State,
            new DecisionAuthorResponse(decision.Author.Type, decision.Author.Id),
            decision.AuthorRoles,
            decision.CompetentRoles,
            decision.ArtifactIds,
            decision.Justification,
            decision.ParentDecisionId,
            decision.CreatedAt);
    }

    public static DecisionPageResponse ToResponse(this DecisionPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return new DecisionPageResponse([.. page.Items.Select(ToResponse)], page.Total, page.Skip, page.Take);
    }

    public static ArtifactTypeRoleResponse ToResponse(this ArtifactTypeRoleDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new ArtifactTypeRoleResponse(entry.ArtifactType, entry.Roles);
    }
}
