using Microsoft.Extensions.Logging.Abstractions;
using SoftwareFactory.Application.Platform;
using SoftwareFactory.Application.Tests.Finops.Fakes;
using SoftwareFactory.Application.Tests.Platform.Fakes;
using SoftwareFactory.Application.Tests.Traceability.Fakes;
using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Application.Traceability.Contracts;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Application.Tests.Traceability;

/// <summary>
/// The public contract of the decision log (HU-003). The point of every case here is the same: what an entry is, and
/// who may close it, are computed from the artifacts touched and the author's hats — never from the caller.
/// </summary>
public sealed class DecisionServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid _tenantId = Guid.CreateVersion7();
    private static readonly Guid _projectId = Guid.CreateVersion7();

    [Fact]
    public async Task An_author_competent_for_what_they_touched_records_a_decision_that_is_born_closed()
    {
        var harness = new Harness(Role.Functional);
        var story = harness.Artifact("user_story", "Registrar pago");

        var decision = await harness.RecordAsync(story);

        Assert.Equal(DecisionNames.Decision, decision.Type);
        Assert.Equal(DecisionNames.Recorded, decision.State);
        Assert.Empty(decision.CompetentRoles);
        Assert.Equal(["functional"], decision.AuthorRoles);
        Assert.Equal(1, harness.UnitOfWork.Commits);

        var audit = Assert.Single(harness.Audit.Events);
        Assert.Equal(AuditAction.DecisionRecorded, audit.Action);
    }

    [Fact]
    public async Task Touching_what_answers_to_another_role_leaves_a_note_pending_that_role()
    {
        var harness = new Harness(Role.Functional);
        var component = harness.Artifact("architecture_component", "Pasarela de pagos");

        var note = await harness.RecordAsync(component);

        Assert.Equal(DecisionNames.OutOfRoleNote, note.Type);
        Assert.Equal(DecisionNames.Pending, note.State);
        Assert.Equal(["architect"], note.CompetentRoles);
        Assert.Single(harness.Decisions.CompetentRoles);
    }

    [Fact]
    public async Task Both_hats_of_a_two_role_author_are_subtracted_and_both_are_recorded()
    {
        var harness = new Harness(Role.Functional, Role.Qa);
        var story = harness.Artifact("user_story", "Registrar pago");
        var test = harness.Artifact("test_case", "El pago baja el saldo");

        var decision = await harness.RecordAsync(story, test);

        // user_story answers to functional and test_case to qa: this author covers both, so nothing is left pending.
        Assert.Equal(DecisionNames.Decision, decision.Type);
        Assert.Empty(decision.CompetentRoles);
        Assert.Equal(["functional", "qa"], decision.AuthorRoles);
        Assert.Equal(2, harness.Decisions.AuthorRoles.Count);
    }

    [Fact]
    public async Task An_author_with_every_role_never_leaves_a_note_pending()
    {
        var harness = new Harness(Enum.GetValues<Role>());
        var component = harness.Artifact("architecture_component", "Pasarela de pagos");
        var compliance = harness.Artifact("compliance_requirement", "Retención de datos");

        var decision = await harness.RecordAsync(component, compliance);

        // Nobody stands above them, so there is nobody to consult: the rule working, not a hole in it (HU-003 §2).
        Assert.Equal(DecisionNames.Decision, decision.Type);
        Assert.Empty(decision.CompetentRoles);
    }

    [Fact]
    public async Task Across_artifacts_only_the_role_the_author_lacks_stays_pending()
    {
        var harness = new Harness(Role.Functional);
        var story = harness.Artifact("user_story", "Registrar pago");
        var component = harness.Artifact("architecture_component", "Pasarela de pagos");

        var note = await harness.RecordAsync(story, component);

        Assert.Equal(DecisionNames.OutOfRoleNote, note.Type);
        Assert.Equal(["architect"], note.CompetentRoles);
    }

    [Fact]
    public async Task A_non_functional_requirement_answers_to_two_roles_and_either_of_them_closes_it()
    {
        var harness = new Harness(Role.Qa);
        var requirement = harness.Artifact("non_functional_requirement", "La respuesta va por debajo de 200 ms");

        var note = await harness.RecordAsync(requirement);
        // The order is the fixed one of the role enum, not the alphabet: functional first, architect second.
        Assert.Equal(["functional", "architect"], note.CompetentRoles);

        harness.SignInAs(Guid.CreateVersion7(), Role.Functional);
        var ratification = await harness.Service.RatifyAsync(new CloseDecisionCommand(note.Id, "El negocio lo sostiene."), CancellationToken.None);

        Assert.Equal(DecisionNames.Ratification, ratification.Type);
        Assert.Equal(note.Id, ratification.ParentDecisionId);
        Assert.Equal(DecisionState.Ratified, harness.Decisions.Decisions.Single(decision => decision.Id == note.Id).State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_note_is_closed_by_ratifying_or_reverting_it_and_the_child_carries_the_same_artifacts(bool ratify)
    {
        var harness = new Harness(Role.Functional);
        var component = harness.Artifact("architecture_component", "Pasarela de pagos");
        var note = await harness.RecordAsync(component);

        harness.SignInAs(Guid.CreateVersion7(), Role.Architect);
        var command = new CloseDecisionCommand(note.Id, "Queda así.");
        var child = ratify
            ? await harness.Service.RatifyAsync(command, CancellationToken.None)
            : await harness.Service.RevertAsync(command, CancellationToken.None);

        Assert.Equal(ratify ? DecisionNames.Ratification : DecisionNames.Reversion, child.Type);
        Assert.Equal(DecisionNames.Recorded, child.State);
        Assert.Equal([component.Id], child.ArtifactIds);
        Assert.Equal(["architect"], child.AuthorRoles);
        Assert.Equal(
            ratify ? DecisionState.Ratified : DecisionState.Reverted,
            harness.Decisions.Decisions.Single(decision => decision.Id == note.Id).State);

        Assert.Contains(
            harness.Audit.Events,
            audit => audit.Action == (ratify ? AuditAction.DecisionRatified : AuditAction.DecisionReverted));
    }

    [Fact]
    public async Task A_role_outside_the_snapshot_cannot_close_the_note()
    {
        var harness = new Harness(Role.Functional);
        var component = harness.Artifact("architecture_component", "Pasarela de pagos");
        var note = await harness.RecordAsync(component);

        harness.SignInAs(Guid.CreateVersion7(), Role.Qa);

        var exception = await Assert.ThrowsAsync<DecisionRoleNotCompetentException>(
            () => harness.Service.RatifyAsync(new CloseDecisionCommand(note.Id, "Me parece bien."), CancellationToken.None));

        Assert.Equal(["architect"], exception.CompetentRoles);
    }

    [Fact]
    public async Task The_author_cannot_close_their_own_note_even_after_gaining_the_competent_role()
    {
        var harness = new Harness(Role.Functional);
        var component = harness.Artifact("architecture_component", "Pasarela de pagos");
        var note = await harness.RecordAsync(component);

        // The same person, promoted: they now clear the role check, and only the identity guard is left (HU-003 §4).
        harness.Author.Roles = [Role.Functional, Role.Architect];

        await Assert.ThrowsAsync<SelfRatificationException>(
            () => harness.Service.RatifyAsync(new CloseDecisionCommand(note.Id, "Ya soy arquitecto."), CancellationToken.None));

        Assert.Equal(DecisionState.Pending, harness.Decisions.Decisions.Single(decision => decision.Id == note.Id).State);
    }

    [Fact]
    public async Task Editing_the_map_afterwards_does_not_move_a_pending_note_to_somebody_else()
    {
        var harness = new Harness(Role.Functional);
        var component = harness.Artifact("architecture_component", "Pasarela de pagos");
        var note = await harness.RecordAsync(component);

        // The tenant decides tomorrow that components answer to QA. The note keeps the owner it was born with.
        harness.Decisions.CompetenceMap.RemoveAll(entry => string.Equals(entry.ArtifactType, "architecture_component", StringComparison.Ordinal));
        harness.Decisions.CompetenceMap.Add(new ArtifactTypeRole(_tenantId, "architecture_component", Role.Qa));

        harness.SignInAs(Guid.CreateVersion7(), Role.Qa);
        await Assert.ThrowsAsync<DecisionRoleNotCompetentException>(
            () => harness.Service.RatifyAsync(new CloseDecisionCommand(note.Id, "Ahora me toca a mí."), CancellationToken.None));

        harness.SignInAs(Guid.CreateVersion7(), Role.Architect);
        var ratification = await harness.Service.RatifyAsync(new CloseDecisionCommand(note.Id, "Sigue siendo mío."), CancellationToken.None);

        Assert.Equal(DecisionNames.Ratification, ratification.Type);
    }

    [Fact]
    public async Task An_artifact_type_with_no_competent_role_refuses_the_decision()
    {
        var harness = new Harness(Role.Functional);
        var orphanType = harness.Artifact("weather_report", "Tipo del catálogo sin mapa");

        var exception = await Assert.ThrowsAsync<ArtifactTypeWithoutCompetentRoleException>(() => harness.RecordAsync(orphanType));

        Assert.Equal(["weather_report"], exception.ArtifactTypes);
        Assert.Empty(harness.Decisions.Decisions);
        Assert.Equal(0, harness.UnitOfWork.Commits);
    }

    [Fact]
    public async Task A_decision_that_bears_on_nothing_is_refused()
    {
        var harness = new Harness(Role.Functional);

        await Assert.ThrowsAsync<ArtifactValidationException>(
            () => harness.Service.RecordAsync(new RecordDecisionCommand(_projectId, [], "Porque sí."), CancellationToken.None));
    }

    [Fact]
    public async Task A_settled_note_is_not_settled_twice()
    {
        var harness = new Harness(Role.Functional);
        var component = harness.Artifact("architecture_component", "Pasarela de pagos");
        var note = await harness.RecordAsync(component);

        harness.SignInAs(Guid.CreateVersion7(), Role.Architect);
        await harness.Service.RatifyAsync(new CloseDecisionCommand(note.Id, "Va."), CancellationToken.None);

        await Assert.ThrowsAsync<DecisionNotPendingException>(
            () => harness.Service.RevertAsync(new CloseDecisionCommand(note.Id, "Me arrepentí."), CancellationToken.None));
    }

    [Fact]
    public async Task Pending_notes_of_a_role_are_listed_from_the_snapshot()
    {
        var harness = new Harness(Role.Functional);
        var component = harness.Artifact("architecture_component", "Pasarela de pagos");
        var story = harness.Artifact("user_story", "Registrar pago");
        var note = await harness.RecordAsync(component);
        await harness.RecordAsync(story);

        var pending = await harness.Service.SearchAsync(
            new DecisionFilter(_projectId) { PendingRole = "architect" },
            CancellationToken.None);

        var only = Assert.Single(pending.Items);
        Assert.Equal(note.Id, only.Id);
    }

    [Fact]
    public async Task An_unknown_role_in_the_filter_is_the_callers_mistake_and_not_an_empty_page()
    {
        var harness = new Harness(Role.Functional);

        await Assert.ThrowsAsync<ArtifactValidationException>(
            () => harness.Service.SearchAsync(new DecisionFilter(_projectId) { PendingRole = "wizard" }, CancellationToken.None));
    }

    [Fact]
    public async Task The_competence_map_is_readable_grouped_by_artifact_type()
    {
        var harness = new Harness(Role.Functional);

        var map = await harness.Service.GetCompetenceMapAsync(CancellationToken.None);

        var requirement = Assert.Single(map, entry => string.Equals(entry.ArtifactType, "non_functional_requirement", StringComparison.Ordinal));
        Assert.Equal(["functional", "architect"], requirement.Roles);
    }

    private sealed class Harness
    {
        public Harness(params Role[] roles)
        {
            Author = new MutableAuthor(Guid.CreateVersion7(), roles);

            foreach (var (artifactType, role) in ArtifactTypeRoleSeed.Base)
            {
                Decisions.CompetenceMap.Add(new ArtifactTypeRole(_tenantId, artifactType, role));
            }

            Service = new DecisionService(
                Decisions,
                Artifacts,
                new AuditTrail(Audit, new FakeClientContext(new AuditClient("127.0.0.1", "xunit"))),
                UnitOfWork,
                new FakeTenantContext(_tenantId),
                Author,
                new FakeClock(_now),
                NullLogger<DecisionService>.Instance);
        }

        public DecisionService Service { get; }

        public MutableAuthor Author { get; }

        public FakeDecisionRepository Decisions { get; } = new();

        public FakeArtifactRepository Artifacts { get; } = new();

        public FakeAuditEventRepository Audit { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; } = new();

        public Artifact Artifact(string type, string title)
        {
            var artifact = new Artifact(_tenantId, _projectId, type, title, ArtifactLevel.Project);
            Artifacts.Artifacts.Add(artifact);
            return artifact;
        }

        public void SignInAs(Guid userId, params Role[] roles)
        {
            Author.AuthorId = userId;
            Author.Roles = roles;
        }

        public Task<DecisionDto> RecordAsync(params Artifact[] artifacts) =>
            Service.RecordAsync(
                new RecordDecisionCommand(_projectId, [.. artifacts.Select(artifact => artifact.Id)], "Porque el alcance cambia."),
                CancellationToken.None);
    }
}
