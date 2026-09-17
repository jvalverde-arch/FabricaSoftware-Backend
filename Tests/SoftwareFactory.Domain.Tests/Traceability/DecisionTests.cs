using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Domain.Tests.Traceability;

/// <summary>
/// The decision log (HU-003). What a decision *is* — a plain decision or a note out of the author's competence — is
/// derived here from the competent roles, never told by the caller; and a note is closed once, by a child decision
/// that points back at it.
/// </summary>
public sealed class DecisionTests
{
    private static readonly Guid _tenant = Guid.CreateVersion7();
    private static readonly Guid _project = Guid.CreateVersion7();
    private static readonly Guid _author = Guid.CreateVersion7();
    private static readonly Guid _ratifier = Guid.CreateVersion7();

    [Fact]
    public void An_author_competent_for_everything_records_a_decision_that_is_born_closed()
    {
        var decision = Record([]);

        Assert.Equal(DecisionType.Decision, decision.Type);
        Assert.Equal(DecisionState.Recorded, decision.State);
        Assert.False(decision.IsPending);
        Assert.Null(decision.ParentDecisionId);
    }

    [Fact]
    public void A_competent_role_the_author_lacks_turns_the_entry_into_a_pending_note()
    {
        var decision = Record([Role.Architect]);

        Assert.Equal(DecisionType.OutOfRoleNote, decision.Type);
        Assert.Equal(DecisionState.Pending, decision.State);
        Assert.True(decision.IsPending);
    }

    [Theory]
    [InlineData(DecisionType.Ratification, DecisionState.Ratified)]
    [InlineData(DecisionType.Reversion, DecisionState.Reverted)]
    public void Closing_a_note_settles_it_and_returns_the_child_that_references_it(DecisionType outcome, DecisionState expected)
    {
        var note = Record([Role.Architect]);

        var child = note.Close(outcome, AuthorType.Human, _ratifier, "La arquitectura lo sostiene.");

        Assert.Equal(expected, note.State);
        Assert.Equal(outcome, child.Type);
        Assert.Equal(DecisionState.Recorded, child.State);
        Assert.Equal(note.Id, child.ParentDecisionId);
        Assert.Equal(note.TenantId, child.TenantId);
        Assert.Equal(note.ProjectId, child.ProjectId);
        Assert.Equal(_ratifier, child.AuthorId);
    }

    [Fact]
    public void A_note_is_closed_only_once()
    {
        var note = Record([Role.Architect]);
        note.Close(DecisionType.Ratification, AuthorType.Human, _ratifier, "Va.");

        var second = Assert.Throws<InvalidOperationException>(
            () => note.Close(DecisionType.Reversion, AuthorType.Human, _ratifier, "Ahora no."));

        Assert.Contains(note.Id.ToString(), second.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_decision_that_was_never_pending_cannot_be_closed()
    {
        var decision = Record([]);

        Assert.Throws<InvalidOperationException>(
            () => decision.Close(DecisionType.Ratification, AuthorType.Human, _ratifier, "Va."));
    }

    [Theory]
    [InlineData(DecisionType.Decision)]
    [InlineData(DecisionType.OutOfRoleNote)]
    public void Only_a_ratification_or_a_reversion_closes_a_note(DecisionType outcome)
    {
        var note = Record([Role.Architect]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => note.Close(outcome, AuthorType.Human, _ratifier, "Va."));
    }

    [Fact]
    public void A_decision_without_justification_is_not_a_decision() =>
        Assert.Throws<ArgumentException>(() => Decision.Record(_tenant, _project, AuthorType.Human, _author, "   ", []));

    [Fact]
    public void An_agent_is_recorded_as_the_author_it_is()
    {
        var decision = Decision.Record(_tenant, _project, AuthorType.Agent, _author, "El caso de prueba cubre la historia.", []);

        Assert.Equal(AuthorType.Agent, decision.AuthorType);
        Assert.Equal(_author, decision.AuthorId);
    }

    private static Decision Record(IReadOnlyCollection<Role> competentRoles) =>
        Decision.Record(_tenant, _project, AuthorType.Human, _author, "El requisito cambia el alcance del módulo.", competentRoles);
}
