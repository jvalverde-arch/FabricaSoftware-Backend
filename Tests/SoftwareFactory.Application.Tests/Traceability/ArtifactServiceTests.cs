using Microsoft.Extensions.Logging.Abstractions;
using SoftwareFactory.Application.Tests.Finops.Fakes;
using SoftwareFactory.Application.Tests.Platform.Fakes;
using SoftwareFactory.Application.Tests.Traceability.Fakes;
using SoftwareFactory.Application.Platform;
using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Application.Traceability.Contracts;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Application.Tests.Traceability;

/// <summary>The public contract of the module (HU-001): create, edit with versioning, list, diff, delete and audit.</summary>
public sealed class ArtifactServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid _tenantId = Guid.CreateVersion7();
    private static readonly Guid _projectId = Guid.CreateVersion7();

    private const string Story = """{"as_a":"cajera","i_want":"registrar un pago","so_that":"la deuda baje","acceptance_criteria":["el saldo baja"]}""";

    [Fact]
    public async Task Creating_an_artifact_writes_its_first_version_and_audits_it()
    {
        var harness = new Harness();

        var created = await harness.Service.CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Registrar pago", Story), CancellationToken.None);

        Assert.Equal("user_story", created.Artifact.Type);
        Assert.Equal(ArtifactNames.Draft, created.Artifact.State);
        Assert.Equal(1, created.Artifact.CurrentVersion);
        Assert.Equal(Story, created.Content);
        Assert.Equal(harness.Schemas.CurrentVersionOf("user_story"), created.SchemaVersion);

        var version = Assert.Single(harness.Repository.Versions);
        Assert.Equal(1, version.Number);
        Assert.Equal(AuthorType.Human, version.AuthorType);

        var audit = Assert.Single(harness.Audit.Events);
        Assert.Equal(AuditAction.ArtifactCreated, audit.Action);
        Assert.Equal(_tenantId, audit.TenantId);
        Assert.Equal(1, harness.UnitOfWork.Commits);
    }

    [Fact]
    public async Task Content_that_breaks_the_schema_is_refused_with_the_offending_fields()
    {
        var harness = new Harness();
        harness.Validator.NextErrors.Add(new SchemaValidationError("soThat", "required"));

        var exception = await Assert.ThrowsAsync<ArtifactValidationException>(
            () => harness.Service.CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Incompleta", """{"as_a":"cajera"}"""), CancellationToken.None));

        Assert.Equal("user_story", exception.ArtifactType);
        Assert.Equal("soThat", Assert.Single(exception.Errors).Path);
        Assert.Empty(harness.Repository.Artifacts);
        Assert.Equal(0, harness.UnitOfWork.Commits);
    }

    [Fact]
    public async Task A_type_outside_the_catalog_is_refused_before_anything_is_written()
    {
        var harness = new Harness();

        await Assert.ThrowsAsync<ArtifactTypeUnknownException>(
            () => harness.Service.CreateAsync(new CreateArtifactCommand(_projectId, "dragon", "Nope", "{}"), CancellationToken.None));

        Assert.Empty(harness.Repository.Artifacts);
    }

    [Fact]
    public async Task Editing_the_content_writes_a_new_version_and_keeps_the_previous_one()
    {
        var harness = new Harness();
        var created = await harness.Service.CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Registrar pago", Story), CancellationToken.None);

        const string edited = """{"as_a":"cajera","i_want":"registrar un pago parcial","so_that":"la deuda baje","acceptance_criteria":["el saldo baja"]}""";
        var updated = await harness.Service.UpdateAsync(new UpdateArtifactCommand(created.Artifact.Id, null, edited), CancellationToken.None);

        Assert.Equal(2, updated.Artifact.CurrentVersion);
        Assert.Equal(edited, updated.Content);
        Assert.Equal(2, harness.Repository.Versions.Count);
        Assert.Equal(Story, harness.Repository.Versions[0].Content);
        Assert.Contains(harness.Audit.Events, e => e.Action == AuditAction.ArtifactUpdated);
    }

    [Fact]
    public async Task Editing_only_the_title_does_not_create_a_version()
    {
        var harness = new Harness();
        var created = await harness.Service.CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Registrar pago", Story), CancellationToken.None);

        var updated = await harness.Service.UpdateAsync(new UpdateArtifactCommand(created.Artifact.Id, "Registrar pago en caja", null), CancellationToken.None);

        Assert.Equal("Registrar pago en caja", updated.Artifact.Title);
        Assert.Equal(1, updated.Artifact.CurrentVersion);
        Assert.Single(harness.Repository.Versions);
    }

    [Fact]
    public async Task Writing_the_same_content_again_does_not_create_a_version()
    {
        var harness = new Harness();
        var created = await harness.Service.CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Registrar pago", Story), CancellationToken.None);

        await harness.Service.UpdateAsync(new UpdateArtifactCommand(created.Artifact.Id, null, Story), CancellationToken.None);

        Assert.Single(harness.Repository.Versions);
    }

    [Fact]
    public async Task A_state_change_is_audited_and_respects_the_machine()
    {
        var harness = new Harness();
        var created = await harness.Service.CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Registrar pago", Story), CancellationToken.None);

        var reviewed = await harness.Service.UpdateAsync(
            new UpdateArtifactCommand(created.Artifact.Id, null, null) { State = ArtifactNames.InReview },
            CancellationToken.None);

        Assert.Equal(ArtifactNames.InReview, reviewed.Artifact.State);
        Assert.Contains(harness.Audit.Events, e => e.Action == AuditAction.ArtifactStateChanged);

        var refused = await Assert.ThrowsAsync<ArtifactStateTransitionException>(() => harness.Service.UpdateAsync(
            new UpdateArtifactCommand(created.Artifact.Id, null, null) { State = ArtifactNames.Frozen },
            CancellationToken.None));
        Assert.Equal(ArtifactNames.InReview, refused.From);
        Assert.Equal(ArtifactNames.Frozen, refused.To);
    }

    [Fact]
    public async Task Reading_returns_the_content_exactly_as_it_was_written()
    {
        var harness = new Harness(currentUserStorySchema: 2);
        var artifact = new Artifact(_tenantId, _projectId, "user_story", "Vieja", ArtifactLevel.Project);
        harness.Repository.Artifacts.Add(artifact);
        harness.Repository.Versions.Add(new ArtifactVersion(_tenantId, artifact.Id, artifact.AdvanceVersion(_now), """{"as_a":"cajera"}""", schemaVersion: 1, AuthorType.Human, Guid.CreateVersion7()));

        var read = await harness.Service.GetAsync(artifact.Id, CancellationToken.None);

        // History is faithful: no upgrade, no re-validation against the newer schema.
        Assert.Equal("""{"as_a":"cajera"}""", read.Content);
        Assert.Equal(1, read.SchemaVersion);
    }

    [Fact]
    public async Task Reading_for_editing_brings_the_content_to_the_current_schema()
    {
        var harness = new Harness(currentUserStorySchema: 2, upgraders: [new AddSoThat()]);
        var artifact = new Artifact(_tenantId, _projectId, "user_story", "Vieja", ArtifactLevel.Project);
        harness.Repository.Artifacts.Add(artifact);
        harness.Repository.Versions.Add(new ArtifactVersion(_tenantId, artifact.Id, artifact.AdvanceVersion(_now), """{"as_a":"cajera"}""", schemaVersion: 1, AuthorType.Human, Guid.CreateVersion7()));

        var editable = await harness.Service.GetForEditingAsync(artifact.Id, CancellationToken.None);

        Assert.Equal(2, editable.SchemaVersion);
        Assert.Contains("so_that", editable.Content, StringComparison.Ordinal);
        // Reading for editing changes nothing in storage: the upgrade is written only when the person saves.
        Assert.Single(harness.Repository.Versions);
        Assert.Equal(1, harness.Repository.Versions[0].SchemaVersion);
    }

    [Fact]
    public async Task Without_an_upgrader_the_old_artifact_can_still_be_read_but_not_edited()
    {
        var harness = new Harness(currentUserStorySchema: 2);
        var artifact = new Artifact(_tenantId, _projectId, "user_story", "Vieja", ArtifactLevel.Project);
        harness.Repository.Artifacts.Add(artifact);
        harness.Repository.Versions.Add(new ArtifactVersion(_tenantId, artifact.Id, artifact.AdvanceVersion(_now), """{"as_a":"cajera"}""", schemaVersion: 1, AuthorType.Human, Guid.CreateVersion7()));

        Assert.Equal(1, (await harness.Service.GetAsync(artifact.Id, CancellationToken.None)).SchemaVersion);
        await Assert.ThrowsAsync<ArtifactSchemaUpgradeUnavailableException>(
            () => harness.Service.GetForEditingAsync(artifact.Id, CancellationToken.None));
    }

    [Fact]
    public async Task A_new_version_is_stamped_with_the_current_schema()
    {
        var harness = new Harness(currentUserStorySchema: 2);
        var created = await harness.Service.CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Nueva", Story), CancellationToken.None);

        Assert.Equal(2, created.SchemaVersion);
        Assert.Equal(2, harness.Repository.Versions[0].SchemaVersion);
    }

    [Fact]
    public async Task The_diff_between_two_versions_lists_what_changed()
    {
        var harness = new Harness();
        var created = await harness.Service.CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Registrar pago", Story), CancellationToken.None);
        await harness.Service.UpdateAsync(
            new UpdateArtifactCommand(created.Artifact.Id, null, """{"as_a":"cajera","i_want":"registrar un pago parcial","so_that":"la deuda baje","acceptance_criteria":["el saldo baja","queda recibo"]}"""),
            CancellationToken.None);

        var diff = await harness.Service.GetDiffAsync(created.Artifact.Id, 1, 2, CancellationToken.None);

        Assert.Equal(1, diff.FromVersion);
        Assert.Equal(2, diff.ToVersion);
        var modified = Assert.Single(diff.Changes, change => change.Path == "i_want");
        Assert.Equal(ArtifactChangeKind.Modified, modified.Kind);
        Assert.Equal("registrar un pago", modified.From);
        Assert.Equal("registrar un pago parcial", modified.To);
        Assert.Contains(diff.Changes, change => change.Path == "acceptance_criteria[1]" && change.Kind == ArtifactChangeKind.Added);
    }

    [Fact]
    public async Task Deleting_is_refused_while_relations_point_at_the_artifact()
    {
        var harness = new Harness();
        var created = await harness.Service.CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Registrar pago", Story), CancellationToken.None);
        harness.Repository.Relations.Add(new ArtifactRelationReference(Guid.CreateVersion7(), "validates", Guid.CreateVersion7(), "Caso de prueba", Incoming: true));

        var exception = await Assert.ThrowsAsync<ArtifactHasRelationsException>(
            () => harness.Service.DeleteAsync(created.Artifact.Id, CancellationToken.None));

        Assert.Equal("Caso de prueba", Assert.Single(exception.Relations).OtherArtifactTitle);
        Assert.False(harness.Repository.Artifacts[0].IsDeleted);
    }

    [Fact]
    public async Task Deleting_without_relations_marks_it_deleted_and_audits_it()
    {
        var harness = new Harness();
        var created = await harness.Service.CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Registrar pago", Story), CancellationToken.None);

        await harness.Service.DeleteAsync(created.Artifact.Id, CancellationToken.None);

        Assert.True(harness.Repository.Artifacts[0].IsDeleted);
        Assert.Contains(harness.Audit.Events, e => e.Action == AuditAction.ArtifactDeleted);
        await Assert.ThrowsAsync<ArtifactNotFoundException>(() => harness.Service.GetAsync(created.Artifact.Id, CancellationToken.None));
    }

    [Fact]
    public async Task The_list_filters_by_type_state_score_and_title()
    {
        var harness = new Harness();
        await harness.Service.CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Registrar pago", Story), CancellationToken.None);
        var second = await harness.Service.CreateAsync(new CreateArtifactCommand(_projectId, "screen", "Pantalla de caja", """{"purpose":"cobrar"}"""), CancellationToken.None);
        await harness.Service.SetScoreAsync(second.Artifact.Id, 90, CancellationToken.None);

        var byType = await harness.Service.SearchAsync(new ArtifactFilter(_projectId) { Type = "screen" }, CancellationToken.None);
        var byTitle = await harness.Service.SearchAsync(new ArtifactFilter(_projectId) { Title = "pago" }, CancellationToken.None);
        var byScore = await harness.Service.SearchAsync(new ArtifactFilter(_projectId) { MinScore = 85 }, CancellationToken.None);

        Assert.Equal("Pantalla de caja", Assert.Single(byType.Items).Title);
        Assert.Equal("Registrar pago", Assert.Single(byTitle.Items).Title);
        Assert.Equal(90, Assert.Single(byScore.Items).Score);
        Assert.Equal(2, (await harness.Service.SearchAsync(new ArtifactFilter(_projectId), CancellationToken.None)).Total);
    }

    [Fact]
    public async Task An_artifact_of_another_tenant_simply_does_not_exist()
    {
        var harness = new Harness();

        await Assert.ThrowsAsync<ArtifactNotFoundException>(() => harness.Service.GetAsync(Guid.CreateVersion7(), CancellationToken.None));
    }

    private sealed class AddSoThat : IArtifactContentUpgrader
    {
        public string ArtifactType => "user_story";

        public int FromVersion => 1;

        public string Upgrade(string content)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(content)!.AsObject();
            node["so_that"] = "por definir";
            return node.ToJsonString();
        }
    }

    private sealed class Harness
    {
        public Harness(int currentUserStorySchema = 1, IEnumerable<IArtifactContentUpgrader>? upgraders = null)
        {
            Schemas = ArtifactSchemaRegistry.ForTesting(
            [
                .. Enumerable.Range(1, currentUserStorySchema).Select(version => new ArtifactSchema("user_story", version, """{"type":"object"}""")),
                new ArtifactSchema("screen", 1, """{"type":"object"}"""),
            ]);

            Service = new ArtifactService(
                Repository,
                new AuditTrail(Audit, new FakeClientContext(new AuditClient("127.0.0.1", "xunit"))),
                UnitOfWork,
                Schemas,
                new ArtifactContentMigrator(Schemas, upgraders ?? []),
                Validator,
                new FakeTenantContext(_tenantId),
                new FakeAuthor(AuthorType.Human, Guid.CreateVersion7()),
                new FakeClock(_now),
                NullLogger<ArtifactService>.Instance);
        }

        public ArtifactService Service { get; }

        public ArtifactSchemaRegistry Schemas { get; }

        public FakeArtifactRepository Repository { get; } = new();

        public FakeAuditEventRepository Audit { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; } = new();

        public PassThroughSchemaValidator Validator { get; } = new();
    }
}
