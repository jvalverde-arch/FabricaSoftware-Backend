using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Application.Traceability.Contracts;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence;
using SoftwareFactory.Infrastructure.Persistence.Repositories;
using SoftwareFactory.Infrastructure.Tests.Persistence;

namespace SoftwareFactory.Infrastructure.Tests.Traceability;

/// <summary>
/// The tree of HU-004 against a real database: the three hierarchical types draw it, the order is total, nothing is
/// hidden, and the filtered mode brings back branches instead of the project.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ProjectTreeTests(PostgresFixture fixture) : IAsyncLifetime
{
    private Guid _tenantId;
    private Guid _projectId;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        await using var admin = fixture.CreateAdminContext();
        _tenantId = await TenantGraph.CreateAsync(admin);
        _projectId = await admin.Projects.Where(project => project.TenantId == _tenantId).Select(project => project.Id).FirstAsync();
        _userId = await admin.Users.Where(user => user.TenantId == _tenantId).Select(user => user.Id).FirstAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task The_three_hierarchical_types_draw_the_tree_and_the_children_come_in_catalog_order()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var module = await ArtifactAsync(context, "module", "Cobranzas");
        var story = await ArtifactAsync(context, "user_story", "Registrar pago");
        var requirement = await ArtifactAsync(context, "functional_requirement", "El saldo baja");
        var test = await ArtifactAsync(context, "test_case", "Pago baja el saldo");
        await RelateAsync(context, story, module, RelationNames.BelongsTo);
        await RelateAsync(context, requirement, story, RelationNames.Implements);
        await RelateAsync(context, test, story, RelationNames.Validates);

        var root = await Service(context).GetAsync(new ProjectTreeQuery(_projectId), CancellationToken.None);
        var moduleNode = Assert.Single(root.Nodes, node => node.Id == module.Id);
        Assert.Equal(1, moduleNode.ChildCount);
        Assert.False(root.Expanded);

        var underModule = await ExpandAsync(context, moduleNode.Path);
        var storyNode = Assert.Single(underModule.Nodes);
        Assert.Equal(story.Id, storyNode.Id);
        Assert.Equal(RelationNames.BelongsTo, storyNode.EdgeType);
        Assert.Equal(2, storyNode.ChildCount);

        var underStory = await ExpandAsync(context, storyNode.Path);

        // implements before validates, which is what puts a story's requirements before its tests (rule 2).
        Assert.Equal([requirement.Id, test.Id], underStory.Nodes.Select(node => node.Id));
        Assert.Equal([RelationNames.Implements, RelationNames.Validates], underStory.Nodes.Select(node => node.EdgeType));
        Assert.Equal($"{module.Id}/{story.Id}/{requirement.Id}", underStory.Nodes[0].Path);
    }

    [Fact]
    public async Task Two_hierarchical_relations_between_the_same_pair_leave_the_edge_of_the_highest_precedence()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var module = await ArtifactAsync(context, "module", "Cobranzas");
        var story = await ArtifactAsync(context, "user_story", "Registrar pago");
        await RelateAsync(context, story, module, RelationNames.Implements);
        await RelateAsync(context, story, module, RelationNames.BelongsTo);

        var children = await ExpandAsync(context, module.Id.ToString());

        var only = Assert.Single(children.Nodes);
        Assert.Equal(RelationNames.BelongsTo, only.EdgeType);
    }

    [Fact]
    public async Task The_order_of_the_children_is_the_same_on_every_call()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var module = await ArtifactAsync(context, "module", "Cobranzas");

        // Same type and same edge on purpose: without the title as the last key the order would be the heap's.
        foreach (var title in new[] { "Registrar pago", "Anular pago", "Consultar saldo", "Emitir recibo" })
        {
            var story = await ArtifactAsync(context, "user_story", title);
            await RelateAsync(context, story, module, RelationNames.BelongsTo);
        }

        var first = await ExpandAsync(context, module.Id.ToString());
        var second = await ExpandAsync(context, module.Id.ToString());

        Assert.Equal(first.Nodes.Select(node => node.Path), second.Nodes.Select(node => node.Path));
        Assert.Equal(["Anular pago", "Consultar saldo", "Emitir recibo", "Registrar pago"], first.Nodes.Select(node => node.Title));
    }

    [Fact]
    public async Task An_artifact_with_two_parents_hangs_under_both_with_a_route_of_its_own()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var cobranzas = await ArtifactAsync(context, "module", "Cobranzas");
        var caja = await ArtifactAsync(context, "module", "Caja");
        var screen = await ArtifactAsync(context, "screen", "Pantalla de pago");
        await RelateAsync(context, screen, cobranzas, RelationNames.BelongsTo);
        await RelateAsync(context, screen, caja, RelationNames.BelongsTo);

        var underCobranzas = await ExpandAsync(context, cobranzas.Id.ToString());
        var underCaja = await ExpandAsync(context, caja.Id.ToString());

        Assert.Equal(screen.Id, Assert.Single(underCobranzas.Nodes).Id);
        Assert.Equal(screen.Id, Assert.Single(underCaja.Nodes).Id);

        // Same artifact, two routes: that is what lets the UI expand one copy without expanding the other.
        Assert.NotEqual(underCobranzas.Nodes[0].Path, underCaja.Nodes[0].Path);
        Assert.Equal($"{cobranzas.Id}/{screen.Id}", underCobranzas.Nodes[0].Path);
    }

    [Fact]
    public async Task An_artifact_with_no_hierarchical_parent_hangs_from_the_unclassified_bucket()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var module = await ArtifactAsync(context, "module", "Cobranzas");
        var loose = await ArtifactAsync(context, "business_rule", "Regla suelta");

        var root = await Service(context).GetAsync(new ProjectTreeQuery(_projectId), CancellationToken.None);
        var bucket = Assert.Single(root.Nodes, node => node.Kind == TreeNodeKinds.Unclassified);

        Assert.Equal(TreeNodeKinds.UnclassifiedId, bucket.Id);
        Assert.Null(bucket.Title);
        Assert.Contains(root.Nodes, node => node.Id == module.Id);

        var inside = await ExpandAsync(context, bucket.Path);
        Assert.Contains(inside.Nodes, node => node.Id == loose.Id);
        Assert.StartsWith($"{TreeNodeKinds.UnclassifiedId}/", inside.Nodes[0].Path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_global_artifact_of_the_tenant_is_not_part_of_the_project_tree()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var module = await ArtifactAsync(context, "module", "Cobranzas");
        var contract = await ArtifactAsync(context, "boundary_contract", "Cobros v1", ArtifactLevel.Global);
        await RelateAsync(context, module, contract, RelationNames.Consumes);

        var root = await Service(context).GetAsync(new ProjectTreeQuery(_projectId), CancellationToken.None);
        var filtered = await FilterAsync(context, new ProjectTreeFilter { Text = "Cobros" });

        Assert.DoesNotContain(root.Nodes, node => node.Id == contract.Id);
        Assert.DoesNotContain(filtered.Nodes, node => node.Id == contract.Id);
        Assert.Equal(0, filtered.MatchCount);
        Assert.Contains(root.Nodes, node => node.Id == module.Id);
    }

    [Fact]
    public async Task A_cycle_does_not_hang_the_query()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var module = await ArtifactAsync(context, "module", "Cobranzas");
        var story = await ArtifactAsync(context, "user_story", "Registrar pago");
        await RelateAsync(context, story, module, RelationNames.BelongsTo);
        await RelateAsync(context, module, story, RelationNames.BelongsTo);

        var underModule = await ExpandAsync(context, module.Id.ToString());
        var storyNode = Assert.Single(underModule.Nodes);

        // The branch never turns back on its own ancestor, and the count does not promise a child it would refuse.
        var underStory = await ExpandAsync(context, storyNode.Path);
        Assert.Equal(0, storyNode.ChildCount);
        Assert.Empty(underStory.Nodes);

        var filtered = await FilterAsync(context, new ProjectTreeFilter { Text = "Registrar" });
        Assert.NotEmpty(filtered.Nodes);
    }

    [Fact]
    public async Task The_filter_brings_back_the_branch_of_each_match_and_not_the_project()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var module = await ArtifactAsync(context, "module", "Cobranzas");
        var story = await ArtifactAsync(context, "user_story", "Registrar pago");
        var test = await ArtifactAsync(context, "test_case", "El saldo baja tras el pago");
        var unrelated = await ArtifactAsync(context, "user_story", "Emitir recibo");
        await RelateAsync(context, story, module, RelationNames.BelongsTo);
        await RelateAsync(context, test, story, RelationNames.Validates);
        await RelateAsync(context, unrelated, module, RelationNames.BelongsTo);

        var filtered = await FilterAsync(context, new ProjectTreeFilter { Text = "saldo" });

        Assert.True(filtered.Expanded);
        Assert.Equal(1, filtered.MatchCount);
        Assert.False(filtered.Truncated);

        // The whole route of the match and nothing else: the sibling story never travels.
        Assert.Equal(
            [module.Id.ToString(), $"{module.Id}/{story.Id}", $"{module.Id}/{story.Id}/{test.Id}"],
            filtered.Nodes.Select(node => node.Path));
        Assert.DoesNotContain(filtered.Nodes, node => node.Id == unrelated.Id);
        Assert.Equal([false, false, true], filtered.Nodes.Select(node => node.Matches));
    }

    [Fact]
    public async Task A_match_without_a_hierarchical_parent_hangs_from_the_bucket_in_filtered_mode_too()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        await ArtifactAsync(context, "module", "Cobranzas");
        var loose = await ArtifactAsync(context, "business_rule", "Regla suelta de cobro");

        var filtered = await FilterAsync(context, new ProjectTreeFilter { Text = "Regla suelta" });

        // Otherwise the user would watch the tree change shape as they type, which is the whole point of rule 3.
        Assert.Equal(
            [TreeNodeKinds.UnclassifiedId.ToString(), $"{TreeNodeKinds.UnclassifiedId}/{loose.Id}"],
            filtered.Nodes.Select(node => node.Path));
        Assert.Equal(TreeNodeKinds.Unclassified, filtered.Nodes[0].Kind);
        Assert.True(filtered.Nodes[1].Matches);
    }

    [Fact]
    public async Task A_filter_wider_than_the_limit_is_cut_on_the_matches_and_says_how_many_there_were()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var module = await ArtifactAsync(context, "module", "Cobranzas");

        foreach (var index in Enumerable.Range(1, 5))
        {
            var story = await ArtifactAsync(context, "user_story", $"Historia de pago {index}");
            await RelateAsync(context, story, module, RelationNames.BelongsTo);
        }

        var filtered = await FilterAsync(context, new ProjectTreeFilter { Text = "Historia de pago" }, maxMatches: 2);

        Assert.True(filtered.Truncated);
        Assert.Equal(5, filtered.MatchCount);

        // Two matches plus the module they hang from: cut on the matches, never on the nodes, so what came back is
        // a whole branch and not a tree with holes.
        Assert.Equal(2, filtered.Nodes.Count(node => node.Matches));
        Assert.Equal(3, filtered.Nodes.Count);
    }

    [Fact]
    public async Task The_tree_of_one_tenant_is_invisible_to_another()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var module = await ArtifactAsync(context, "module", "Cobranzas");

        Guid otherTenantId;
        await using (var admin = fixture.CreateAdminContext())
        {
            otherTenantId = await TenantGraph.CreateAsync(admin);
        }

        await using var intruder = fixture.CreateAppContext(otherTenantId);
        var service = new ProjectTreeService(
            new ProjectTreeRepository(intruder),
            new FixedTenantContext(otherTenantId),
            Options.Create(new ProjectTreeOptions()));

        var root = await service.GetAsync(new ProjectTreeQuery(_projectId), CancellationToken.None);
        var filtered = await service.GetAsync(
            new ProjectTreeQuery(_projectId) { Filter = new ProjectTreeFilter { Text = "Cobranzas" } },
            CancellationToken.None);

        Assert.DoesNotContain(root.Nodes, node => node.Id == module.Id);
        Assert.Empty(filtered.Nodes);
        Assert.Equal(0, filtered.MatchCount);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task The_filtered_mode_answers_under_200_ms_with_10k_artifacts()
    {
        await SeedSyntheticProjectAsync(artifacts: 10_000);

        await using var context = fixture.CreateAppContext(_tenantId);
        var service = Service(context);
        var filter = new ProjectTreeQuery(_projectId) { Filter = new ProjectTreeFilter { Text = "Sintética 1" } };

        // Warm the plan cache and the buffers: the number that matters is a query on a working system, not the first
        // one after a bulk load.
        await service.GetAsync(filter, CancellationToken.None);

        var stopwatch = Stopwatch.StartNew();
        var tree = await service.GetAsync(filter, CancellationToken.None);
        stopwatch.Stop();

        Assert.NotEmpty(tree.Nodes);
        Assert.True(
            stopwatch.ElapsedMilliseconds < 200,
            $"The filtered mode took {stopwatch.ElapsedMilliseconds} ms over {tree.MatchCount} matches (the target of HU-004 is 200 ms).");
    }

    /// <summary>
    /// Builds the synthetic project in the database itself: ten modules and the rest of the artifacts hanging off
    /// them, so the filter has 10k titles to look through and the ancestor closure has a route to climb.
    /// </summary>
    private async Task SeedSyntheticProjectAsync(int artifacts)
    {
        await using var admin = fixture.CreateAdminContext();

        // Bulk loading is not the measurement; it only has to finish.
        admin.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        var tenant = _tenantId.ToString("D");
        await admin.Database.ExecuteSqlAsync($"SELECT set_config('app.tenant_id', {tenant}, false)");

        await admin.Database.ExecuteSqlAsync($"""
            INSERT INTO artifact (id, tenant_id, project_id, type, title, state, level, score, current_version, created_at, updated_at)
            SELECT gen_random_uuid(), {_tenantId}, {_projectId}, 'module', 'Módulo sintético ' || n, 'draft', 'project', NULL, 0, now(), now()
            FROM generate_series(1, 10) AS n
            """);

        await admin.Database.ExecuteSqlAsync($"""
            INSERT INTO artifact (id, tenant_id, project_id, type, title, state, level, score, current_version, created_at, updated_at)
            SELECT gen_random_uuid(), {_tenantId}, {_projectId}, 'user_story', 'Sintética ' || n, 'draft', 'project', NULL, 0, now(), now()
            FROM generate_series(1, {artifacts - 10}) AS n
            """);

        await admin.Database.ExecuteSqlAsync($"""
            WITH modules AS (
                SELECT a.id, row_number() OVER (ORDER BY a.id) - 1 AS position, count(*) OVER () AS total
                FROM artifact a
                WHERE a.project_id = {_projectId} AND a.title LIKE 'Módulo sintético %'
            ),
            stories AS (
                SELECT a.id, row_number() OVER (ORDER BY a.id) - 1 AS position
                FROM artifact a
                WHERE a.project_id = {_projectId} AND a.title LIKE 'Sintética %'
            )
            INSERT INTO relation (id, tenant_id, source_id, target_id, type, metadata, created_by_type, created_by, created_at)
            SELECT gen_random_uuid(), {_tenantId}, stories.id, modules.id, 'belongs_to', NULL, 'human', {_userId}, now()
            FROM stories
            JOIN modules ON modules.position = stories.position % modules.total
            ON CONFLICT DO NOTHING
            """);

        // Fresh statistics: right after a bulk load the planner still believes the tables are small and picks plans
        // for a project that no longer exists. A real load ends the same way, with ANALYZE.
        await admin.Database.ExecuteSqlRawAsync("ANALYZE artifact, relation");
    }

    private Task<ProjectTreeDto> ExpandAsync(SoftwareFactoryDbContext context, string node) =>
        Service(context).GetAsync(new ProjectTreeQuery(_projectId) { Node = node }, CancellationToken.None);

    private Task<ProjectTreeDto> FilterAsync(SoftwareFactoryDbContext context, ProjectTreeFilter filter, int maxMatches = 500) =>
        Service(context, maxMatches).GetAsync(new ProjectTreeQuery(_projectId) { Filter = filter }, CancellationToken.None);

    private async Task<Artifact> ArtifactAsync(
        SoftwareFactoryDbContext context,
        string type,
        string title,
        ArtifactLevel level = ArtifactLevel.Project)
    {
        var artifact = new Artifact(_tenantId, _projectId, type, title, level);
        context.Artifacts.Add(artifact);
        await context.SaveChangesAsync();

        return artifact;
    }

    private async Task RelateAsync(SoftwareFactoryDbContext context, Artifact source, Artifact target, string type)
    {
        context.Relations.Add(new Relation(_tenantId, source.Id, target.Id, type, metadata: null, AuthorType.Human, _userId));
        await context.SaveChangesAsync();
    }

    private ProjectTreeService Service(SoftwareFactoryDbContext context, int maxMatches = 500) =>
        new(
            new ProjectTreeRepository(context),
            new FixedTenantContext(_tenantId),
            Options.Create(new ProjectTreeOptions { MaxMatches = maxMatches }));
}
