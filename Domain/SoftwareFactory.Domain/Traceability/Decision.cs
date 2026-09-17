using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Traceability;

/// <summary>
/// Entry of the decision log (doc 01, principles 6-7): who decided what and why. Whether an entry is a plain decision
/// or a note out of the author's competence is derived, never declared (HU-003 §1-2) — which is what keeps an agent
/// from naming its own ratifier. The roles are not here but in two snapshots beside it: an author may wear several
/// hats and storing one would mean the server picking a hat for them, which is falsifying the log.
/// </summary>
public sealed class Decision : TenantScopedEntity
{
    private Decision()
    {
    }

    private Decision(
        Guid tenantId,
        Guid projectId,
        DecisionType type,
        AuthorType authorType,
        Guid authorId,
        string justification,
        Guid? parentDecisionId,
        DecisionState state)
        : base(tenantId)
    {
        Guard.NotEmpty(projectId);
        Guard.NotEmpty(authorId);
        ProjectId = projectId;
        Type = type;
        AuthorType = authorType;
        AuthorId = authorId;
        Justification = Guard.NotBlank(justification);
        ParentDecisionId = parentDecisionId;
        State = state;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid ProjectId { get; private set; }

    public DecisionType Type { get; private set; }

    public AuthorType AuthorType { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Justification { get; private set; } = string.Empty;

    public DecisionState State { get; private set; }

    /// <summary>The decision this one ratifies or reverts, when applicable.</summary>
    public Guid? ParentDecisionId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>True while the note is waiting for a competent role to ratify or revert it.</summary>
    public bool IsPending => State == DecisionState.Pending;

    /// <summary>
    /// Records a decision. The competent roles are the ones left over from the map of the artifacts touched once all
    /// the author's roles are taken out (HU-003 §2): empty means the author's hats covered everything they touched and
    /// the entry is born closed; anything left means the entry is a note somebody else has to settle.
    /// </summary>
    public static Decision Record(
        Guid tenantId,
        Guid projectId,
        AuthorType authorType,
        Guid authorId,
        string justification,
        IReadOnlyCollection<Role> competentRoles)
    {
        ArgumentNullException.ThrowIfNull(competentRoles);

        var (type, state) = competentRoles.Count == 0
            ? (DecisionType.Decision, DecisionState.Recorded)
            : (DecisionType.OutOfRoleNote, DecisionState.Pending);

        return new Decision(tenantId, projectId, type, authorType, authorId, justification, null, state);
    }

    /// <summary>
    /// Closes a pending note with the child decision that references it (HU-003 §4). Who may close it — a role in the
    /// snapshot, and never the author — is checked before getting here; what this guarantees is that a note is settled
    /// once, and only while it is still pending.
    /// </summary>
    public Decision Close(DecisionType outcome, AuthorType authorType, Guid authorId, string justification)
    {
        if (outcome is not (DecisionType.Ratification or DecisionType.Reversion))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "A note is closed by ratifying or reverting it.");
        }

        if (!IsPending)
        {
            throw new InvalidOperationException($"The decision {Id} is {State} and no longer waits for anybody.");
        }

        State = outcome == DecisionType.Ratification ? DecisionState.Ratified : DecisionState.Reverted;

        return new Decision(TenantId, ProjectId, outcome, authorType, authorId, justification, Id, DecisionState.Recorded);
    }
}
