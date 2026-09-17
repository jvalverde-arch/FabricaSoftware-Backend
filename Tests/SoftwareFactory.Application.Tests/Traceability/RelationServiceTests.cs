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

/// <summary>The public contract of the module for relations (HU-002): matrix, cross-project rule, audit.</summary>
public sealed class RelationServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid _tenantId = Guid.CreateVersion7();
    private static readonly Guid _projectId = Guid.CreateVersion7();
    private static readonly Guid _otherProjectId = Guid.CreateVersion7();

    [Fact]
    public async Task A_compatible_relation_is_created_and_audited()
    {
        var harness = new Harness();
        var story = harness.Artifact("user_story", "Registrar pago");
        var test = harness.Artifact("test_case", "Pago baja el saldo");

        var relation = await harness.Service.CreateAsync(
            new CreateRelationCommand(test.Id, story.Id, RelationNames.Validates),
            CancellationToken.None);

        Assert.Equal(test.Id, relation.SourceId);
        Assert.Equal(story.Id, relation.TargetId);
        Assert.Equal(RelationNames.Validates, relation.Type);
        Assert.Equal(ArtifactNames.Human, relation.CreatedBy.Type);
        Assert.Single(harness.Relations.Relations);

        var audit = Assert.Single(harness.Audit.Events);
        Assert.Equal(AuditAction.RelationCreated, audit.Action);
        Assert.Equal(1, harness.UnitOfWork.Commits);
    }

    [Fact]
    public async Task An_incompatible_relation_is_refused_with_the_rule_it_broke()
    {
        var harness = new Harness();
        var story = harness.Artifact("user_story", "Registrar pago");
        var test = harness.Artifact("test_case", "Pago baja el saldo");

        // Backwards: the test validates the story, not the other way round.
        var exception = await Assert.ThrowsAsync<RelationIncompatibleException>(() => harness.Service.CreateAsync(
            new CreateRelationCommand(story.Id, test.Id, RelationNames.Validates),
            CancellationToken.None));

        Assert.Equal("user_story", exception.SourceType);
        Assert.Equal(RelationNames.Validates, exception.RelationType);
        Assert.Equal("test_case", exception.TargetType);
        Assert.Empty(exception.AllowedTargets);
        Assert.Empty(harness.Relations.Relations);
        Assert.Equal(0, harness.UnitOfWork.Commits);
    }

    [Fact]
    public async Task A_relation_type_outside_the_catalog_never_reaches_the_artifacts()
    {
        var harness = new Harness();

        await Assert.ThrowsAsync<RelationTypeUnknownException>(() => harness.Service.CreateAsync(
            new CreateRelationCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), "inspires"),
            CancellationToken.None));

        Assert.Empty(harness.Relations.Relations);
    }

    [Fact]
    public async Task Metadata_travels_with_the_relation_and_has_to_be_json()
    {
        var harness = new Harness();
        var module = harness.Artifact("module", "Cobranzas");
        var screen = harness.Artifact("screen", "Caja");

        var relation = await harness.Service.CreateAsync(
            new CreateRelationCommand(screen.Id, module.Id, RelationNames.BelongsTo) { Metadata = """{"note":"vista principal"}""" },
            CancellationToken.None);

        Assert.Equal("""{"note":"vista principal"}""", relation.Metadata);

        var invalid = await Assert.ThrowsAsync<ArtifactValidationException>(() => harness.Service.CreateAsync(
            new CreateRelationCommand(module.Id, screen.Id, RelationNames.BelongsTo) { Metadata = "no es json" },
            CancellationToken.None));

        Assert.Equal("metadata", Assert.Single(invalid.Errors).Path);
    }

    [Fact]
    public async Task The_same_relation_twice_is_refused()
    {
        var harness = new Harness();
        var module = harness.Artifact("module", "Cobranzas");
        var screen = harness.Artifact("screen", "Caja");
        var command = new CreateRelationCommand(screen.Id, module.Id, RelationNames.BelongsTo);
        await harness.Service.CreateAsync(command, CancellationToken.None);

        await Assert.ThrowsAsync<RelationAlreadyExistsException>(() => harness.Service.CreateAsync(command, CancellationToken.None));
        Assert.Single(harness.Relations.Relations);
    }

    [Fact]
    public async Task An_artifact_cannot_be_related_to_itself()
    {
        var harness = new Harness();
        var module = harness.Artifact("module", "Cobranzas");

        await Assert.ThrowsAsync<RelationSelfReferenceException>(() => harness.Service.CreateAsync(
            new CreateRelationCommand(module.Id, module.Id, RelationNames.DependsOn),
            CancellationToken.None));
    }

    [Fact]
    public async Task An_artifact_that_does_not_exist_is_a_404_before_anything_else()
    {
        var harness = new Harness();
        var module = harness.Artifact("module", "Cobranzas");

        await Assert.ThrowsAsync<ArtifactNotFoundException>(() => harness.Service.CreateAsync(
            new CreateRelationCommand(module.Id, Guid.CreateVersion7(), RelationNames.DependsOn),
            CancellationToken.None));
    }

    [Fact]
    public async Task A_module_of_one_project_consumes_a_global_boundary_contract_of_another()
    {
        var harness = new Harness();
        var module = harness.Artifact("module", "Cobranzas");
        var contract = harness.Artifact("boundary_contract", "API de pagos v1", _otherProjectId, ArtifactLevel.Global);

        var relation = await harness.Service.CreateAsync(
            new CreateRelationCommand(module.Id, contract.Id, RelationNames.Consumes),
            CancellationToken.None);

        // Crossing projects is exactly what a boundary contract is for (HU-002 §6).
        Assert.Equal(RelationNames.Consumes, relation.Type);
    }

    [Fact]
    public async Task Two_project_artifacts_of_different_projects_cannot_be_related()
    {
        var harness = new Harness();
        var mine = harness.Artifact("module", "Cobranzas");
        var theirs = harness.Artifact("module", "Facturación", _otherProjectId);

        var exception = await Assert.ThrowsAsync<RelationCrossProjectException>(() => harness.Service.CreateAsync(
            new CreateRelationCommand(mine.Id, theirs.Id, RelationNames.DependsOn),
            CancellationToken.None));

        Assert.Equal(mine.Id, exception.SourceId);
        Assert.Equal(theirs.Id, exception.TargetId);
        Assert.Empty(harness.Relations.Relations);
    }

    [Fact]
    public async Task Deleting_a_relation_audits_it()
    {
        var harness = new Harness();
        var module = harness.Artifact("module", "Cobranzas");
        var screen = harness.Artifact("screen", "Caja");
        var relation = await harness.Service.CreateAsync(
            new CreateRelationCommand(screen.Id, module.Id, RelationNames.BelongsTo),
            CancellationToken.None);

        await harness.Service.DeleteAsync(screen.Id, relation.Id, CancellationToken.None);

        Assert.Empty(harness.Relations.Relations);
        Assert.Equal(AuditAction.RelationDeleted, harness.Audit.Events[^1].Action);
    }

    [Fact]
    public async Task A_relation_of_another_artifact_is_not_this_artifact_s_to_delete()
    {
        var harness = new Harness();
        var module = harness.Artifact("module", "Cobranzas");
        var screen = harness.Artifact("screen", "Caja");
        var stranger = harness.Artifact("screen", "Reportes");
        var relation = await harness.Service.CreateAsync(
            new CreateRelationCommand(screen.Id, module.Id, RelationNames.BelongsTo),
            CancellationToken.None);

        await Assert.ThrowsAsync<RelationNotFoundException>(() => harness.Service.DeleteAsync(stranger.Id, relation.Id, CancellationToken.None));
        Assert.Single(harness.Relations.Relations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task The_neighborhood_goes_from_one_to_three_levels(int levels)
    {
        var harness = new Harness();
        var module = harness.Artifact("module", "Cobranzas");

        var exception = await Assert.ThrowsAsync<RelationLevelsOutOfRangeException>(
            () => harness.Service.GetNeighborhoodAsync(module.Id, levels, CancellationToken.None));

        Assert.Equal(RelationService.MaxLevels, exception.MaxLevels);
    }

    [Fact]
    public async Task Looking_for_orphans_of_a_relation_outside_the_catalog_is_refused()
    {
        var harness = new Harness();

        await Assert.ThrowsAsync<RelationTypeUnknownException>(
            () => harness.Service.GetOrphansAsync(_projectId, "user_story", "inspires", CancellationToken.None));
    }

    private sealed class Harness
    {
        public Harness()
        {
            Service = new RelationService(
                Relations,
                Artifacts,
                new AuditTrail(Audit, new FakeClientContext(new AuditClient("127.0.0.1", "xunit"))),
                UnitOfWork,
                RelationCompatibilityMatrix.Embedded,
                new FakeTenantContext(_tenantId),
                new FakeAuthor(AuthorType.Human, Guid.CreateVersion7()),
                new FakeClock(_now),
                NullLogger<RelationService>.Instance);
        }

        public RelationService Service { get; }

        public FakeRelationRepository Relations { get; } = new();

        public FakeArtifactRepository Artifacts { get; } = new();

        public FakeAuditEventRepository Audit { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; } = new();

        public Artifact Artifact(string type, string title, Guid? projectId = null, ArtifactLevel level = ArtifactLevel.Project)
        {
            var artifact = new Artifact(_tenantId, projectId ?? _projectId, type, title, level);
            Artifacts.Artifacts.Add(artifact);
            return artifact;
        }
    }
}
