using SoftwareFactory.Application.Tests.Finops.Fakes;
using SoftwareFactory.Application.Tests.Traceability.Fakes;
using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Application.Traceability.Contracts;
using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Application.Tests.Traceability;

/// <summary>
/// The artifact card (HU-005). What it owns is composition: which version it shows, which schema it pairs with that
/// version, and what it can say about a relation before somebody follows it.
/// </summary>
public sealed class ArtifactCardServiceTests
{
    private static readonly Guid _tenantId = Guid.CreateVersion7();
    private static readonly Guid _projectId = Guid.CreateVersion7();
    private static readonly Guid _otherProjectId = Guid.CreateVersion7();
    private static readonly Guid _artifactId = Guid.CreateVersion7();
    private static readonly Guid _moduleId = Guid.CreateVersion7();
    private static readonly Guid _otherModuleId = Guid.CreateVersion7();

    private const string SchemaV1 = """{"type":"object","properties":{"statement":{"type":"string"}}}""";
    private const string SchemaV2 = """{"type":"object","properties":{"rule":{"type":"string"}}}""";

    [Fact]
    public async Task The_card_shows_the_current_version_with_the_schema_of_that_version()
    {
        var harness = new Harness();

        var card = await harness.Service.GetAsync(new ArtifactCardQuery(_artifactId), CancellationToken.None);

        Assert.Equal(2, card.Version);
        Assert.Equal(2, card.SchemaVersion);
        Assert.Equal(SchemaV2, card.Schema.Json);
        Assert.Equal("""{"rule":"No se cobra dos veces"}""", card.Content);
        Assert.Empty(harness.Artifacts.VersionsAsked);
    }

    [Fact]
    public async Task An_older_version_is_drawn_with_its_own_schema_and_not_with_todays()
    {
        var harness = new Harness();

        var card = await harness.Service.GetAsync(new ArtifactCardQuery(_artifactId) { Version = 1 }, CancellationToken.None);

        // The whole point: version 1 was written against schema 1, whose field is «statement». Pairing it with
        // schema 2 would label it «rule» — a name that did not exist when somebody wrote this — and say nothing.
        Assert.Equal(1, card.Version);
        Assert.Equal(1, card.SchemaVersion);
        Assert.Equal(SchemaV1, card.Schema.Json);
        Assert.Equal("""{"statement":"No se cobra dos veces"}""", card.Content);
        Assert.Equal([1], harness.Artifacts.VersionsAsked);
    }

    [Fact]
    public async Task Relations_come_grouped_by_direction_and_by_type()
    {
        var harness = new Harness();
        harness.Relations.Rows.Add(Row(upstream: true, RelationNames.BelongsTo, _moduleId, "module", "Cobranzas", _moduleId));
        harness.Relations.Rows.Add(Row(upstream: false, RelationNames.Validates, Guid.CreateVersion7(), "test_case", "El saldo baja", _moduleId));
        harness.Relations.Rows.Add(Row(upstream: false, RelationNames.Validates, Guid.CreateVersion7(), "test_case", "Se emite recibo", _moduleId));

        var card = await harness.Service.GetAsync(new ArtifactCardQuery(_artifactId), CancellationToken.None);

        var up = Assert.Single(card.Upstream);
        Assert.Equal(RelationNames.BelongsTo, up.Type);
        Assert.Single(up.Items);

        var down = Assert.Single(card.Downstream);
        Assert.Equal(RelationNames.Validates, down.Type);
        Assert.Equal(2, down.Items.Count);
    }

    [Fact]
    public async Task A_relation_that_stays_in_the_module_is_not_marked_as_a_crossing()
    {
        var harness = new Harness();
        harness.Relations.Rows.Add(Row(upstream: true, RelationNames.BelongsTo, _moduleId, "module", "Cobranzas", _moduleId));
        harness.Relations.Rows.Add(Row(upstream: false, RelationNames.Validates, Guid.CreateVersion7(), "test_case", "El saldo baja", _moduleId));

        var card = await harness.Service.GetAsync(new ArtifactCardQuery(_artifactId), CancellationToken.None);

        Assert.All(card.Upstream.Concat(card.Downstream).SelectMany(group => group.Items), item =>
        {
            Assert.False(item.CrossesModule);
            Assert.False(item.CrossesProject);
        });
    }

    [Fact]
    public async Task A_relation_into_another_module_or_another_project_is_marked_and_names_where_it_lands()
    {
        var harness = new Harness();
        harness.Relations.Rows.Add(Row(upstream: true, RelationNames.BelongsTo, _moduleId, "module", "Cobranzas", _moduleId));
        harness.Relations.Rows.Add(Row(upstream: false, RelationNames.DependsOn, Guid.CreateVersion7(), "user_story", "Historia vecina", _otherModuleId));
        harness.Relations.Rows.Add(Row(
            upstream: true,
            RelationNames.Consumes,
            Guid.CreateVersion7(),
            "boundary_contract",
            "Cobros v1",
            moduleId: null,
            projectId: _otherProjectId,
            projectName: "Tesorería"));

        var card = await harness.Service.GetAsync(new ArtifactCardQuery(_artifactId), CancellationToken.None);
        var items = card.Upstream.Concat(card.Downstream).SelectMany(group => group.Items).ToList();

        var neighbour = Assert.Single(items, item => item.Title == "Historia vecina");
        Assert.True(neighbour.CrossesModule);
        Assert.False(neighbour.CrossesProject);

        var contract = Assert.Single(items, item => item.Title == "Cobros v1");
        Assert.True(contract.CrossesProject);
        Assert.Equal("Tesorería", contract.ProjectName);

        // Nobody filed it under a module, so there is no module to leave — an unknown is not a crossing.
        Assert.False(contract.CrossesModule);
    }

    [Fact]
    public async Task The_decisions_asked_for_are_the_ones_of_this_artifact()
    {
        var harness = new Harness();

        await harness.Service.GetAsync(new ArtifactCardQuery(_artifactId), CancellationToken.None);

        Assert.Equal(_artifactId, harness.Decisions.Asked?.ArtifactId);
        Assert.Equal(_projectId, harness.Decisions.Asked?.ProjectId);
    }

    private static CardRelationRow Row(
        bool upstream,
        string type,
        Guid otherId,
        string otherType,
        string title,
        Guid? moduleId,
        Guid? projectId = null,
        string projectName = "Cobranzas del cliente") =>
        new(
            Guid.CreateVersion7(),
            type,
            upstream,
            otherId,
            otherType,
            title,
            ArtifactState.Draft,
            ArtifactLevel.Project,
            null,
            projectId ?? _projectId,
            projectName,
            moduleId,
            moduleId is null ? null : "Cobranzas");

    private sealed class Harness
    {
        public Harness()
        {
            var artifact = new ArtifactDto(
                _artifactId,
                _projectId,
                "business_rule",
                "No se cobra dos veces",
                ArtifactNames.Draft,
                ArtifactNames.ProjectLevel,
                null,
                2,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch);

            var author = new ArtifactAuthor(ArtifactNames.Human, Guid.CreateVersion7());

            Artifacts.Current = new ArtifactDetailDto(artifact, """{"rule":"No se cobra dos veces"}""", 2, author);
            Artifacts.ByVersion[1] = new ArtifactDetailDto(artifact, """{"statement":"No se cobra dos veces"}""", 1, author);
            Artifacts.Versions.Add(new ArtifactVersionDto(1, 1, author, DateTimeOffset.UnixEpoch));
            Artifacts.Versions.Add(new ArtifactVersionDto(2, 2, author, DateTimeOffset.UnixEpoch));

            Service = new ArtifactCardService(
                Artifacts,
                Decisions,
                Relations,
                ArtifactSchemaRegistry.ForTesting(
                [
                    new ArtifactSchema("business_rule", 1, SchemaV1),
                    new ArtifactSchema("business_rule", 2, SchemaV2),
                ]),
                new FakeTenantContext(_tenantId));
        }

        public ArtifactCardService Service { get; }

        public StubArtifactService Artifacts { get; } = new();

        public StubDecisionService Decisions { get; } = new();

        public FakeArtifactCardRepository Relations { get; } = new();
    }
}
