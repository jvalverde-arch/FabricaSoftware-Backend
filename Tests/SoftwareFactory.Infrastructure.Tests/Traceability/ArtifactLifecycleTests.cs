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
using SoftwareFactory.Infrastructure.Traceability;
using SoftwareFactory.Infrastructure.Tests.Persistence;

namespace SoftwareFactory.Infrastructure.Tests.Traceability;

/// <summary>
/// The cycle of HU-001 against a real database: create → edit → version → diff, the logical delete blocked by
/// relations, and the tenant isolation that keeps one customer's model invisible to another.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ArtifactLifecycleTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string Story = """{"as_a":"cajera","i_want":"registrar un pago","so_that":"la deuda baje","acceptance_criteria":["el saldo baja"]}""";

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
    public async Task Create_edit_version_and_diff_survive_a_round_trip_through_the_database()
    {
        Guid artifactId;

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            var service = Service(context);
            var created = await service.CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Registrar pago", Story), CancellationToken.None);
            artifactId = created.Artifact.Id;

            await service.UpdateAsync(
                new UpdateArtifactCommand(artifactId, "Registrar pago en caja", """{"as_a":"cajera","i_want":"registrar un pago parcial","so_that":"la deuda baje","acceptance_criteria":["el saldo baja","queda recibo"]}"""),
                CancellationToken.None);
        }

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            var service = Service(context);

            var read = await service.GetAsync(artifactId, CancellationToken.None);
            Assert.Equal("Registrar pago en caja", read.Artifact.Title);
            Assert.Equal(2, read.Artifact.CurrentVersion);
            Assert.Equal(1, read.SchemaVersion);
            Assert.Contains("parcial", read.Content, StringComparison.Ordinal);

            var versions = await service.GetVersionsAsync(artifactId, CancellationToken.None);
            Assert.Equal([1, 2], versions.Select(version => version.Number));
            Assert.All(versions, version => Assert.Equal(ArtifactNames.Human, version.Author.Type));

            var diff = await service.GetDiffAsync(artifactId, 1, 2, CancellationToken.None);
            Assert.Contains(diff.Changes, change => change.Path == "i_want" && change.Kind == ArtifactChangeKind.Modified);
            Assert.Contains(diff.Changes, change => change.Path == "acceptance_criteria[1]" && change.Kind == ArtifactChangeKind.Added);
        }
    }

    [Fact]
    public async Task The_content_is_stored_as_jsonb_and_can_be_queried_by_field()
    {
        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            await Service(context).CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Consulta jsonb", Story), CancellationToken.None);
        }

        await using var query = fixture.CreateAppContext(_tenantId);

        // The GIN index on content exists for this: the brain and the matrix will search inside the document.
        var found = await query.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM artifact_version WHERE content @> '{{\"as_a\":\"cajera\"}}'::jsonb")
            .SingleAsync();

        Assert.True(found >= 1);
    }

    [Fact]
    public async Task Deleting_is_refused_while_a_relation_points_at_the_artifact_and_allowed_once_it_is_gone()
    {
        Guid storyId;
        Guid relationId;

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            var service = Service(context);
            var story = await service.CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Con prueba", Story), CancellationToken.None);
            var test = await service.CreateAsync(
                new CreateArtifactCommand(_projectId, "test_case", "Verifica el pago", """{"kind":"e2e","steps":["pagar"],"expected_result":"saldo baja"}"""),
                CancellationToken.None);

            storyId = story.Artifact.Id;
            var relation = new Relation(_tenantId, test.Artifact.Id, storyId, "validates", null, AuthorType.Human, _userId);
            context.Relations.Add(relation);
            await context.SaveChangesAsync();
            relationId = relation.Id;
        }

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            var exception = await Assert.ThrowsAsync<ArtifactHasRelationsException>(
                () => Service(context).DeleteAsync(storyId, CancellationToken.None));

            var reference = Assert.Single(exception.Relations);
            Assert.Equal("validates", reference.Type);
            Assert.Equal("Verifica el pago", reference.OtherArtifactTitle);
            Assert.Equal("incoming", reference.Direction);
        }

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            await context.Relations.Where(relation => relation.Id == relationId).ExecuteDeleteAsync();
        }

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            await Service(context).DeleteAsync(storyId, CancellationToken.None);
        }

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            await Assert.ThrowsAsync<ArtifactNotFoundException>(() => Service(context).GetAsync(storyId, CancellationToken.None));
            // The row is still there: the delete is logical, nothing is lost (HU-001 §4).
            Assert.True(await context.Artifacts.AnyAsync(artifact => artifact.Id == storyId && artifact.DeletedAt != null));
        }
    }

    [Fact]
    public async Task The_list_filters_by_type_state_title_and_module()
    {
        Guid moduleId;

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            var service = Service(context);
            var module = await service.CreateAsync(
                new CreateArtifactCommand(_projectId, "module", "Cobros", """{"name":"Cobros","purpose":"Gestiona la cobranza"}"""),
                CancellationToken.None);
            moduleId = module.Artifact.Id;

            var inside = await service.CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Pago en caja", Story), CancellationToken.None);
            await service.CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Devolución", Story), CancellationToken.None);

            context.Relations.Add(new Relation(_tenantId, inside.Artifact.Id, moduleId, "belongs_to", null, AuthorType.Human, _userId));
            await context.SaveChangesAsync();
        }

        await using var query = fixture.CreateAppContext(_tenantId);
        var service2 = Service(query);

        var stories = await service2.SearchAsync(new ArtifactFilter(_projectId) { Type = "user_story" }, CancellationToken.None);
        var byTitle = await service2.SearchAsync(new ArtifactFilter(_projectId) { Title = "devoluc" }, CancellationToken.None);
        var byModule = await service2.SearchAsync(new ArtifactFilter(_projectId) { ModuleId = moduleId }, CancellationToken.None);
        var drafts = await service2.SearchAsync(new ArtifactFilter(_projectId) { State = ArtifactNames.Draft }, CancellationToken.None);

        // The probe graph of the fixture already put artifacts in this project, so this checks what the test created.
        Assert.Contains(stories.Items, artifact => artifact.Title == "Pago en caja");
        Assert.Contains(stories.Items, artifact => artifact.Title == "Devolución");
        Assert.All(stories.Items, artifact => Assert.Equal("user_story", artifact.Type));
        Assert.Equal("Devolución", Assert.Single(byTitle.Items).Title);
        Assert.Equal("Pago en caja", Assert.Single(byModule.Items).Title);
        Assert.True(drafts.Total >= 3);
    }

    [Fact]
    public async Task An_artifact_of_another_tenant_is_invisible_even_knowing_its_id()
    {
        Guid foreignArtifact;
        Guid otherTenant;

        await using (var admin = fixture.CreateAdminContext())
        {
            otherTenant = await TenantGraph.CreateAsync(admin);
        }

        await using (var context = fixture.CreateAppContext(otherTenant))
        {
            var projectId = await context.Projects.Select(project => project.Id).FirstAsync();
            var created = await Service(context, otherTenant).CreateAsync(
                new CreateArtifactCommand(projectId, "user_story", "De otro cliente", Story),
                CancellationToken.None);
            foreignArtifact = created.Artifact.Id;
        }

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            await Assert.ThrowsAsync<ArtifactNotFoundException>(() => Service(context).GetAsync(foreignArtifact, CancellationToken.None));
            Assert.Equal(0, await context.Artifacts.CountAsync(artifact => artifact.Id == foreignArtifact));
        }
    }

    [Fact]
    public async Task Every_change_leaves_an_audit_event()
    {
        Guid artifactId;

        await using (var context = fixture.CreateAppContext(_tenantId))
        {
            var service = Service(context);
            var created = await service.CreateAsync(new CreateArtifactCommand(_projectId, "user_story", "Auditada", Story), CancellationToken.None);
            artifactId = created.Artifact.Id;
            await service.UpdateAsync(new UpdateArtifactCommand(artifactId, null, null) { State = ArtifactNames.InReview }, CancellationToken.None);
            await service.DeleteAsync(artifactId, CancellationToken.None);
        }

        await using var query = fixture.CreateAppContext(_tenantId);
        var actions = await query.AuditEvents
            .Where(auditEvent => auditEvent.ActorId == _userId)
            .Select(auditEvent => auditEvent.Action)
            .ToListAsync();

        Assert.Contains(AuditAction.ArtifactCreated, actions);
        Assert.Contains(AuditAction.ArtifactStateChanged, actions);
        Assert.Contains(AuditAction.ArtifactDeleted, actions);
    }

    private ArtifactService Service(SoftwareFactoryDbContext context, Guid? tenantId = null) =>
        new(
            new ArtifactRepository(context),
            new AuditTrail(new AuditEventRepository(context), new FixedClient()),
            new UnitOfWork(context),
            ArtifactSchemaRegistry.Embedded,
            new ArtifactContentMigrator(ArtifactSchemaRegistry.Embedded, []),
            new NJsonSchemaValidator(),
            new FixedTenantContext(tenantId ?? _tenantId),
            new FixedAuthor(_userId),
            TimeProvider.System,
            NullLogger<ArtifactService>.Instance);

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
