using Microsoft.EntityFrameworkCore;
using SoftwareFactory.Application.Traceability.Contracts;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Project;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence;
using SoftwareFactory.Infrastructure.Persistence.Repositories;
using SoftwareFactory.Infrastructure.Tests.Persistence;

namespace SoftwareFactory.Infrastructure.Tests.Traceability;

/// <summary>
/// The relation panel of the card against a real database (HU-005 §2). What cannot be faked here is the join that
/// says where a relation lands: its project, its module, and therefore whether following it leaves either.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ArtifactCardTests(PostgresFixture fixture) : IAsyncLifetime
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
    public async Task What_hangs_from_the_same_module_is_not_a_crossing()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var module = await ArtifactAsync(context, "module", "Cobranzas");
        var story = await ArtifactAsync(context, "user_story", "Registrar pago");
        var test = await ArtifactAsync(context, "test_case", "El saldo baja");
        await RelateAsync(context, story, module, RelationNames.BelongsTo);
        await RelateAsync(context, test, module, RelationNames.BelongsTo);
        await RelateAsync(context, test, story, RelationNames.Validates);

        var rows = await new ArtifactCardRepository(context).GetRelationsAsync(story.Id, CancellationToken.None);

        var up = Assert.Single(rows, row => row.Upstream);
        Assert.Equal(module.Id, up.OtherId);
        // A module is its own module, so the story reads as staying inside it.
        Assert.Equal(module.Id, up.OtherModuleId);

        var down = Assert.Single(rows, row => !row.Upstream);
        Assert.Equal(test.Id, down.OtherId);
        Assert.Equal(module.Id, down.OtherModuleId);
        Assert.Equal("Cobranzas", down.OtherModuleTitle);
        Assert.Equal(_projectId, down.OtherProjectId);
    }

    [Fact]
    public async Task A_relation_into_another_module_carries_that_module()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var cobranzas = await ArtifactAsync(context, "module", "Cobranzas");
        var caja = await ArtifactAsync(context, "module", "Caja");
        var mine = await ArtifactAsync(context, "user_story", "Registrar pago");
        var neighbour = await ArtifactAsync(context, "user_story", "Arqueo de caja");
        await RelateAsync(context, mine, cobranzas, RelationNames.BelongsTo);
        await RelateAsync(context, neighbour, caja, RelationNames.BelongsTo);
        await RelateAsync(context, mine, neighbour, RelationNames.DependsOn);

        var rows = await new ArtifactCardRepository(context).GetRelationsAsync(mine.Id, CancellationToken.None);
        var dependency = Assert.Single(rows, row => row.RelationType == RelationNames.DependsOn);

        Assert.Equal(caja.Id, dependency.OtherModuleId);
        Assert.Equal("Caja", dependency.OtherModuleTitle);
    }

    [Fact]
    public async Task A_relation_into_another_project_carries_that_projects_name()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var module = await ArtifactAsync(context, "module", "Cobranzas");

        var neighbour = new SoftwareProject(_tenantId, "Tesorería", "El proyecto de al lado");
        context.Projects.Add(neighbour);
        await context.SaveChangesAsync();

        var contract = new Artifact(_tenantId, neighbour.Id, "boundary_contract", "Cobros v1", ArtifactLevel.Global);
        context.Artifacts.Add(contract);
        await context.SaveChangesAsync();
        await RelateAsync(context, module, contract, RelationNames.Consumes);

        var rows = await new ArtifactCardRepository(context).GetRelationsAsync(module.Id, CancellationToken.None);
        var consumed = Assert.Single(rows, row => row.RelationType == RelationNames.Consumes);

        // Following this leaves the project, and the card can say where it lands before anybody clicks.
        Assert.Equal(neighbour.Id, consumed.OtherProjectId);
        Assert.Equal("Tesorería", consumed.OtherProjectName);
        Assert.Equal(ArtifactLevel.Global, consumed.OtherLevel);
    }

    [Fact]
    public async Task A_deleted_artifact_is_not_on_the_card()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var module = await ArtifactAsync(context, "module", "Cobranzas");
        var story = await ArtifactAsync(context, "user_story", "Registrar pago");
        await RelateAsync(context, story, module, RelationNames.BelongsTo);

        story.Delete(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();

        var rows = await new ArtifactCardRepository(context).GetRelationsAsync(module.Id, CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task The_card_of_one_tenant_is_invisible_to_another()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var module = await ArtifactAsync(context, "module", "Cobranzas");
        var story = await ArtifactAsync(context, "user_story", "Registrar pago");
        await RelateAsync(context, story, module, RelationNames.BelongsTo);

        Guid otherTenantId;
        await using (var admin = fixture.CreateAdminContext())
        {
            otherTenantId = await TenantGraph.CreateAsync(admin);
        }

        await using var intruder = fixture.CreateAppContext(otherTenantId);
        var rows = await new ArtifactCardRepository(intruder).GetRelationsAsync(story.Id, CancellationToken.None);

        Assert.Empty(rows);
    }

    private async Task<Artifact> ArtifactAsync(SoftwareFactoryDbContext context, string type, string title)
    {
        var artifact = new Artifact(_tenantId, _projectId, type, title, ArtifactLevel.Project);
        context.Artifacts.Add(artifact);
        await context.SaveChangesAsync();

        return artifact;
    }

    private async Task RelateAsync(SoftwareFactoryDbContext context, Artifact source, Artifact target, string type)
    {
        context.Relations.Add(new Relation(_tenantId, source.Id, target.Id, type, metadata: null, AuthorType.Human, _userId));
        await context.SaveChangesAsync();
    }
}
