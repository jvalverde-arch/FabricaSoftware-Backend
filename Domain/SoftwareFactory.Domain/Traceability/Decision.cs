using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Traceability;

/// <summary>Entry of the decision log (doc 01, principles 6-7): who decided what, in which role, and why.</summary>
public sealed class Decision : TenantScopedEntity
{
    private Decision()
    {
    }

    public Decision(Guid tenantId, Guid projectId, DecisionType type, AuthorType authorType, Guid authorId, Role role, string justification, Guid? parentDecisionId)
        : base(tenantId)
    {
        Guard.NotEmpty(projectId);
        Guard.NotEmpty(authorId);
        ProjectId = projectId;
        Type = type;
        AuthorType = authorType;
        AuthorId = authorId;
        Role = role;
        Justification = Guard.NotBlank(justification);
        ParentDecisionId = parentDecisionId;
        State = type == DecisionType.OutOfRoleNote ? DecisionState.Pending : DecisionState.Recorded;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid ProjectId { get; private set; }

    public DecisionType Type { get; private set; }

    public AuthorType AuthorType { get; private set; }

    public Guid AuthorId { get; private set; }

    public Role Role { get; private set; }

    public string Justification { get; private set; } = string.Empty;

    public DecisionState State { get; private set; }

    /// <summary>The decision this one ratifies or reverts, when applicable.</summary>
    public Guid? ParentDecisionId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
