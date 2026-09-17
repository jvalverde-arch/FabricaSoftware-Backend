using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Traceability;

/// <summary>
/// One role that may ratify or revert a pending note, stored as a snapshot taken when the note was recorded
/// (HU-003 §2). It is a snapshot on purpose: editing the competence map afterwards must not silently change who
/// owns a note that is already waiting.
/// </summary>
public sealed class DecisionCompetentRole : TenantScopedEntity
{
    private DecisionCompetentRole()
    {
    }

    public DecisionCompetentRole(Guid tenantId, Guid decisionId, Role role)
        : base(tenantId)
    {
        Guard.NotEmpty(decisionId);
        DecisionId = decisionId;
        Role = role;
    }

    public Guid DecisionId { get; private set; }

    public Role Role { get; private set; }
}
