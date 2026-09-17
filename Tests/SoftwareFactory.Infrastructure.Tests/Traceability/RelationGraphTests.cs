using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareFactory.Application.Platform;
using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Application.Traceability.Contracts;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence;
using SoftwareFactory.Infrastructure.Persistence.Repositories;
using SoftwareFactory.Infrastructure.Tests.Persistence;

namespace SoftwareFactory.Infrastructure.Tests.Traceability;

/// <summary>
/// The graph of HU-002 against a real database: the neighborhood walk with cycles, the orphan query, the
/// cross-project rule and the tenant isolation that no application check could give.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class RelationGraphTests(PostgresFixture fixture) : IAsyncLifetime
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
    public async Task The_neighborhood_grows_one_ring_per_level()
    {
        // module <- belongs_to - story <- validates - test, plus a screen hanging off the module.
        await using var context = fixture.CreateAppContext(_tenantId);
        var module = await CreateAsync(context, "module", "Cobranzas");
        var story = await CreateAsync(context, "user_story", "Registrar pago");
        var test = await CreateAsync(context, "test_case", "El saldo baja");
        var screen = await CreateAsync(context, "screen", "Caja");
        var service = Service(context);

        await service.CreateAsync(new CreateRelationCommand(story.Id, module.Id, RelationNames.BelongsTo), CancellationToken.None);
        await service.CreateAsync(new CreateRelationCommand(test.Id, story.Id, RelationNames.Validates), CancellationToken.None);
        await service.CreateAsync(new CreateRelationCommand(screen.Id, module.Id, RelationNames.BelongsTo), CancellationToken.None);

        var one = await service.GetNeighborhoodAsync(module.Id, 1, CancellationToken.None);
        Assert.Equal(new[] { module.Id, screen.Id, story.Id }.Order(), one.Nodes.Select(node => node.Id).Order());
        Assert.Equal(0, one.Nodes.Single(node => node.Id == module.Id).Depth);
        Assert.Equal(1, one.Nodes.Single(node => node.Id == story.Id).Depth);

        var two = await service.GetNeighborhoodAsync(module.Id, 2, CancellationToken.None);
        Assert.Contains(two.Nodes, node => node.Id == test.Id && node.Depth == 2);
        Assert.Equal(3, two.Edges.Count);

        var three = await service.GetNeighborhoodAsync(module.Id, 3, CancellationToken.None);
        Assert.Equal(two.Nodes.Count, three.Nodes.Count);
    }

    [Fact]
    public async Task A_cycle_does_not_hang_the_walk()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var first = await CreateAsync(context, "module", "Uno");
        var second = await CreateAsync(context, "module", "Dos");
        var third = await CreateAsync(context, "module", "Tres");
        var service = Service(context);

        // A depends_on B depends_on C depends_on A: a cycle the walk has to terminate on by depth alone.
        await service.CreateAsync(new CreateRelationCommand(first.Id, second.Id, RelationNames.DependsOn), CancellationToken.None);
        await service.CreateAsync(new CreateRelationCommand(second.Id, third.Id, RelationNames.DependsOn), CancellationToken.None);
        await service.CreateAsync(new CreateRelationCommand(third.Id, first.Id, RelationNames.DependsOn), CancellationToken.None);

        var neighborhood = await service.GetNeighborhoodAsync(first.Id, 3, CancellationToken.None);

        Assert.Equal(new[] { first.Id, second.Id, third.Id }.Order(), neighborhood.Nodes.Select(node => node.Id).Order());
        Assert.Equal(0, neighborhood.Nodes.Single(node => node.Id == first.Id).Depth);
        // Each node appears once, with the shortest distance: the cycle does not push anybody further away.
        Assert.All(neighborhood.Nodes.Where(node => node.Id != first.Id), node => Assert.Equal(1, node.Depth));
        Assert.Equal(3, neighborhood.Edges.Count);
    }

    [Fact]
    public async Task A_deleted_artifact_is_not_part_of_anybody_s_neighborhood()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var module = await CreateAsync(context, "module", "Cobranzas");
        var screen = await CreateAsync(context, "screen", "Caja");
        var service = Service(context);
        await service.CreateAsync(new CreateRelationCommand(screen.Id, module.Id, RelationNames.BelongsTo), CancellationToken.None);

        screen.Delete(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();

        var neighborhood = await service.GetNeighborhoodAsync(module.Id, 2, CancellationToken.None);

        Assert.DoesNotContain(neighborhood.Nodes, node => node.Id == screen.Id);
    }

    [Fact]
    public async Task Orphans_are_the_artifacts_of_a_type_with_no_relation_of_the_asked_type()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var covered = await CreateAsync(context, "user_story", "Con prueba");
        var orphan = await CreateAsync(context, "user_story", "Sin prueba");
        var test = await CreateAsync(context, "test_case", "Cubre la primera");
        var service = Service(context);
        await service.CreateAsync(new CreateRelationCommand(test.Id, covered.Id, RelationNames.Validates), CancellationToken.None);

        var orphans = await service.GetOrphansAsync(_projectId, "user_story", RelationNames.Validates, CancellationToken.None);

        Assert.Contains(orphans, artifact => artifact.Id == orphan.Id);
        Assert.DoesNotContain(orphans, artifact => artifact.Id == covered.Id);
    }

    [Fact]
    public async Task A_module_consumes_a_global_contract_of_another_project_and_the_neighborhood_crosses_with_it()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var otherProject = new Domain.Project.SoftwareProject(_tenantId, "Otro proyecto", "Vecino");
        context.Projects.Add(otherProject);
        await context.SaveChangesAsync();

        var module = await CreateAsync(context, "module", "Cobranzas");
        var contract = await CreateAsync(context, "boundary_contract", "API de pagos v1", otherProject.Id, ArtifactLevel.Global);
        var theirModule = await CreateAsync(context, "module", "Facturación", otherProject.Id);
        var service = Service(context);

        await service.CreateAsync(new CreateRelationCommand(module.Id, contract.Id, RelationNames.Consumes), CancellationToken.None);
        await service.CreateAsync(new CreateRelationCommand(theirModule.Id, contract.Id, RelationNames.Exposes), CancellationToken.None);

        // Two hops away lives a module of another project: that is the point of a boundary contract (HU-002 §6).
        var neighborhood = await service.GetNeighborhoodAsync(module.Id, 2, CancellationToken.None);
        Assert.Contains(neighborhood.Nodes, node => node.Id == theirModule.Id && node.ProjectId == otherProject.Id);

        await Assert.ThrowsAsync<RelationCrossProjectException>(() => service.CreateAsync(
            new CreateRelationCommand(module.Id, theirModule.Id, RelationNames.DependsOn),
            CancellationToken.None));
    }

    [Fact]
    public async Task The_neighborhood_of_another_tenant_is_empty_even_with_its_identifier_in_hand()
    {
        Guid strangerId;
        Guid otherTenantId;

        await using (var admin = fixture.CreateAdminContext())
        {
            otherTenantId = await TenantGraph.CreateAsync(admin);
            strangerId = await admin.Artifacts
                .Where(artifact => artifact.TenantId == otherTenantId && artifact.Type == "user_story")
                .Select(artifact => artifact.Id)
                .FirstAsync();
        }

        await using var context = fixture.CreateAppContext(_tenantId);
        var service = Service(context);

        // Row-level security, not an application check: the artifact simply is not there for this tenant.
        await Assert.ThrowsAsync<ArtifactNotFoundException>(() => service.GetNeighborhoodAsync(strangerId, 2, CancellationToken.None));
    }

    [Fact]
    public async Task A_relation_cannot_reach_into_another_tenant()
    {
        Guid strangerId;

        await using (var admin = fixture.CreateAdminContext())
        {
            var otherTenantId = await TenantGraph.CreateAsync(admin);
            strangerId = await admin.Artifacts
                .Where(artifact => artifact.TenantId == otherTenantId && artifact.Type == "user_story")
                .Select(artifact => artifact.Id)
                .FirstAsync();
        }

        await using var context = fixture.CreateAppContext(_tenantId);
        var test = await CreateAsync(context, "test_case", "Intenta cruzar");

        await Assert.ThrowsAsync<ArtifactNotFoundException>(() => Service(context).CreateAsync(
            new CreateRelationCommand(test.Id, strangerId, RelationNames.Validates),
            CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task The_neighborhood_answers_under_200_ms_with_10k_artifacts_and_50k_relations()
    {
        var root = await SeedSyntheticGraphAsync(artifacts: 10_000, relations: 50_000);

        await using var context = fixture.CreateAppContext(_tenantId);
        var service = Service(context);

        // Warm the plan cache and the buffers: the number that matters is a query on a working system, not the
        // first one after a bulk load.
        await service.GetNeighborhoodAsync(root, 3, CancellationToken.None);

        var stopwatch = Stopwatch.StartNew();
        var neighborhood = await service.GetNeighborhoodAsync(root, 3, CancellationToken.None);
        stopwatch.Stop();
        Assert.NotEmpty(neighborhood.Nodes);
        Assert.True(
            stopwatch.ElapsedMilliseconds < 200,
            $"The neighborhood to 3 levels took {stopwatch.ElapsedMilliseconds} ms over {neighborhood.Nodes.Count} nodes (the target of HU-002 is 200 ms).");
    }

    /// <summary>
    /// Builds the synthetic graph in the database itself: 10k artifacts and 50k relations inserted one row at a time
    /// through EF would measure the test, not the query.
    /// </summary>
    private async Task<Guid> SeedSyntheticGraphAsync(int artifacts, int relations)
    {
        await using var admin = fixture.CreateAdminContext();
        // Bulk loading is not the measurement; it only has to finish.
        admin.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        var tenant = _tenantId.ToString("D");
        await admin.Database.ExecuteSqlAsync($"SELECT set_config('app.tenant_id', {tenant}, false)");

        await admin.Database.ExecuteSqlAsync($"""
            INSERT INTO artifact (id, tenant_id, project_id, type, title, state, level, score, current_version, created_at, updated_at)
            SELECT gen_random_uuid(), {_tenantId}, {_projectId}, 'user_story', 'Sintética ' || n, 'draft', 'project', NULL, 0, now(), now()
            FROM generate_series(1, {artifacts}) AS n
            """);

        // A ring plus fixed chords: five edges per node, every node reachable, and the shape is built by index
        // arithmetic instead of a random join, which would be O(n²) and would measure the seeding, not the query.
        var chords = relations / artifacts;
        await admin.Database.ExecuteSqlAsync($"""
            WITH numbered AS (
                SELECT a.id, row_number() OVER (ORDER BY a.id) - 1 AS position, count(*) OVER () AS total
                FROM artifact a
                WHERE a.project_id = {_projectId} AND a.title LIKE 'Sintética %'
            )
            INSERT INTO relation (id, tenant_id, source_id, target_id, type, metadata, created_by_type, created_by, created_at)
            SELECT gen_random_uuid(), {_tenantId}, source.id, target.id, 'depends_on', NULL, 'human', {_userId}, now()
            FROM numbered AS source
            JOIN generate_series(1, {chords}) AS delta ON TRUE
            JOIN numbered AS target ON target.position = (source.position + delta * delta) % source.total
            WHERE source.id <> target.id
            ON CONFLICT DO NOTHING
            """);

        // Fresh statistics: right after a bulk load the planner still believes the tables are small and picks plans
        // for a graph that no longer exists. A real load ends the same way, with ANALYZE.
        await admin.Database.ExecuteSqlRawAsync("ANALYZE artifact, relation");

        return await admin.Artifacts
            .Where(artifact => artifact.ProjectId == _projectId && artifact.Title == "Sintética 1")
            .Select(artifact => artifact.Id)
            .FirstAsync();
    }

    private async Task<Artifact> CreateAsync(SoftwareFactoryDbContext context, string type, string title, Guid? projectId = null, ArtifactLevel level = ArtifactLevel.Project)
    {
        var artifact = new Artifact(_tenantId, projectId ?? _projectId, type, title, level);
        context.Artifacts.Add(artifact);
        await context.SaveChangesAsync();

        return artifact;
    }

    private RelationService Service(SoftwareFactoryDbContext context) =>
        new(
            new RelationRepository(context),
            new ArtifactRepository(context),
            new AuditTrail(new AuditEventRepository(context), new FixedClient()),
            new UnitOfWork(context),
            RelationCompatibilityMatrix.Embedded,
            new FixedTenantContext(_tenantId),
            new FixedAuthor(_userId),
            TimeProvider.System,
            NullLogger<RelationService>.Instance);

    private sealed class FixedAuthor(Guid userId) : IArtifactAuthorContext
    {
        public AuthorType AuthorType => AuthorType.Human;

        public Guid AuthorId { get; } = userId;
    }

    private sealed class FixedClient : Application.Common.Security.IClientContext
    {
        public AuditClient Client { get; } = new("127.0.0.1", "xunit");
    }
}
