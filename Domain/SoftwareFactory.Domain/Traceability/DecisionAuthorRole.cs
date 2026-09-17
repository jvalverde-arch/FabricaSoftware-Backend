using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Traceability;

/// <summary>
/// One of the roles the author held when the decision was recorded (HU-003 §1), stored as a snapshot. Symmetric with
/// <see cref="DecisionCompetentRole"/> and for the same reason: a user's roles change, and what was written down
/// must not change with them.
/// </summary>
public sealed class DecisionAuthorRole : TenantScopedEntity
{
    private DecisionAuthorRole()
    {
    }

    public DecisionAuthorRole(Guid tenantId, Guid decisionId, Role role)
        : base(tenantId)
    {
        Guard.NotEmpty(decisionId);
        DecisionId = decisionId;
        Role = role;
    }

    public Guid DecisionId { get; private set; }

    public Role Role { get; private set; }
}
