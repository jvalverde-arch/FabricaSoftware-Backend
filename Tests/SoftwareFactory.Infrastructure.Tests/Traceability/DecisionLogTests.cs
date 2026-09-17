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
/// The decision log of HU-003 against a real database: the two snapshots survive the map being edited, the queries
/// read what was stored and not what the configuration says today, and no tenant sees another's log.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class DecisionLogTests(PostgresFixture fixture) : IAsyncLifetime
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
    public async Task A_note_travels_from_pending_to_ratified_with_both_snapshots_stored()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var component = await CreateAsync(context, "architecture_component", "Pasarela de pagos");
        var author = new MutableAuthor(_userId, Role.Functional);

        var note = await Service(context, author).RecordAsync(
            new RecordDecisionCommand(_projectId, [component.Id], "El componente cambia el alcance del módulo."),
            CancellationToken.None);

        Assert.Equal(DecisionNames.OutOfRoleNote, note.Type);
        Assert.Equal(DecisionNames.Pending, note.State);
        Assert.Equal(["architect"], note.CompetentRoles);
        Assert.Equal(["functional"], note.AuthorRoles);

        var architect = new MutableAuthor(Guid.CreateVersion7(), Role.Architect);
        var ratification = await Service(context, architect).RatifyAsync(
            new CloseDecisionCommand(note.Id, "La arquitectura lo sostiene."),
            CancellationToken.None);

        Assert.Equal(DecisionNames.Ratification, ratification.Type);
        Assert.Equal(note.Id, ratification.ParentDecisionId);
        Assert.Equal([component.Id], ratification.ArtifactIds);

        await using var fresh = fixture.CreateAppContext(_tenantId);
        Assert.Equal(DecisionState.Ratified, await fresh.Decisions.Where(d => d.Id == note.Id).Select(d => d.State).SingleAsync());
        Assert.Equal(2, await fresh.DecisionArtifacts.CountAsync(link => link.ArtifactId == component.Id));
        Assert.Equal(1, await fresh.DecisionCompetentRoles.CountAsync(role => role.DecisionId == note.Id));

        var audited = await fresh.AuditEvents
            .Where(audit => audit.Action == AuditAction.DecisionRecorded || audit.Action == AuditAction.DecisionRatified)
            .CountAsync();
        Assert.Equal(2, audited);
    }

    [Fact]
    public async Task Editing_the_map_afterwards_does_not_move_a_note_that_is_already_pending()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var component = await CreateAsync(context, "architecture_component", "Cola de eventos");
        var author = new MutableAuthor(_userId, Role.Functional);

        var note = await Service(context, author).RecordAsync(
            new RecordDecisionCommand(_projectId, [component.Id], "Hay que partir la cola."),
            CancellationToken.None);

        // The tenant decides that components now answer to QA. The stored snapshot is what governs the note.
        await context.ArtifactTypeRoles
            .Where(map => map.ArtifactType == "architecture_component")
            .ExecuteDeleteAsync();
        context.ArtifactTypeRoles.Add(new ArtifactTypeRole(_tenantId, "architecture_component", Role.Qa));
        await context.SaveChangesAsync();

        await using var fresh = fixture.CreateAppContext(_tenantId);
        var pendingForArchitect = await Service(fresh, author).SearchAsync(
            new DecisionFilter(_projectId) { PendingRole = "architect" },
            CancellationToken.None);
        var pendingForQa = await Service(fresh, author).SearchAsync(
            new DecisionFilter(_projectId) { PendingRole = "qa" },
            CancellationToken.None);

        Assert.Contains(pendingForArchitect.Items, item => item.Id == note.Id);
        Assert.DoesNotContain(pendingForQa.Items, item => item.Id == note.Id);
    }

    [Fact]
    public async Task The_log_of_one_tenant_is_invisible_to_another()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var component = await CreateAsync(context, "architecture_component", "Bus de integración");
        var author = new MutableAuthor(_userId, Role.Functional);

        var note = await Service(context, author).RecordAsync(
            new RecordDecisionCommand(_projectId, [component.Id], "El bus cambia de dueño."),
            CancellationToken.None);

        Guid otherTenantId;
        await using (var admin = fixture.CreateAdminContext())
        {
            otherTenantId = await TenantGraph.CreateAsync(admin);
        }

        await using var intruder = fixture.CreateAppContext(otherTenantId);
        Assert.False(await intruder.Decisions.AnyAsync(decision => decision.Id == note.Id));
        Assert.False(await intruder.DecisionCompetentRoles.AnyAsync(role => role.DecisionId == note.Id));
        Assert.False(await intruder.DecisionAuthorRoles.AnyAsync(role => role.DecisionId == note.Id));
        Assert.False(await intruder.DecisionArtifacts.AnyAsync(link => link.DecisionId == note.Id));
    }

    [Fact]
    public async Task Every_artifact_type_of_the_catalog_has_a_competent_role_in_a_provisioned_tenant()
    {
        await using var context = fixture.CreateAppContext(_tenantId);

        var mapped = await context.ArtifactTypeRoles.Select(map => map.ArtifactType).Distinct().ToListAsync();

        // Otherwise the log would fail closed on a type nobody can own — which is the refusal of HU-003 §2.
        Assert.Equal(ArtifactSchemaRegistry.Embedded.Types.Order(), mapped.Order());
    }

    private async Task<Artifact> CreateAsync(SoftwareFactoryDbContext context, string type, string title)
    {
        var artifact = new Artifact(_tenantId, _projectId, type, title, ArtifactLevel.Project);
        context.Artifacts.Add(artifact);
        await context.SaveChangesAsync();

        return artifact;
    }

    private DecisionService Service(SoftwareFactoryDbContext context, IArtifactAuthorContext author) =>
        new(
            new DecisionRepository(context),
            new ArtifactRepository(context),
            new AuditTrail(new AuditEventRepository(context), new FixedClient()),
            new UnitOfWork(context),
            new FixedTenantContext(_tenantId),
            author,
            TimeProvider.System,
            NullLogger<DecisionService>.Instance);

    private sealed class MutableAuthor(Guid userId, params Role[] roles) : IArtifactAuthorContext
    {
        public AuthorType AuthorType => AuthorType.Human;

        public Guid AuthorId { get; } = userId;

        public IReadOnlyCollection<Role> Roles { get; } = roles;
    }

    private sealed class FixedClient : Application.Common.Security.IClientContext
    {
        public AuditClient Client { get; } = new("127.0.0.1", "xunit");
    }
}
