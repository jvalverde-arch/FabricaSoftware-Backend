using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareFactory.Application.Common.Persistence;
using SoftwareFactory.Application.Project;
using SoftwareFactory.Application.Project.Contracts;
using SoftwareFactory.Domain.Project;
using SoftwareFactory.Infrastructure.Persistence;
using SoftwareFactory.Infrastructure.Persistence.Repositories;
using SoftwareFactory.Infrastructure.Tests.Persistence;

namespace SoftwareFactory.Infrastructure.Tests.Project;

/// <summary>
/// Projects against a real database (HU-007). The two things that cannot be proven with a fake are here: the unique
/// index settling a race between two writers, and row-level security answering «not found» to another tenant.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ProjectServiceTests(PostgresFixture fixture) : IAsyncLifetime
{
    private Guid _tenantId;

    public async Task InitializeAsync()
    {
        await using var admin = fixture.CreateAdminContext();
        _tenantId = await TenantGraph.CreateAsync(admin);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_project_is_created_and_comes_back_in_the_list()
    {
        await using var context = fixture.CreateAppContext(_tenantId);
        var service = Service(context, _tenantId);

        var created = await service.CreateAsync(new CreateProjectCommand("Cobranzas", "Cobrar lo que se debe"), CancellationToken.None);

        await using var fresh = fixture.CreateAppContext(_tenantId);
        var page = await Service(fresh, _tenantId).SearchAsync(new ProjectFilter(), CancellationToken.None);

        Assert.Contains(page.Items, project => project.Id == created.Id && project.Name == "Cobranzas");
        Assert.Equal(created.Id, (await Service(fresh, _tenantId).GetAsync(created.Id, CancellationToken.None)).Id);
    }

    [Fact]
    public async Task Two_writers_claiming_the_same_name_leave_one_row_and_the_loser_is_told_the_name_is_taken()
    {
        await using var first = fixture.CreateAppContext(_tenantId);
        await using var second = fixture.CreateAppContext(_tenantId);

        // Both pass the courtesy check — nobody owns the name yet — and both try to write. The unique index is what
        // settles it, which is the whole point of estandar-backend.md §4.
        first.Projects.Add(new SoftwareProject(_tenantId, "Tesorería", null));
        second.Projects.Add(new SoftwareProject(_tenantId, "Tesorería", null));

        var results = await Task.WhenAll(
            Commit(new UnitOfWork(first)),
            Commit(new UnitOfWork(second)));

        Assert.Equal(1, results.Count(refusal => refusal is null));
        var refused = Assert.Single(results.OfType<UniqueConstraintViolationException>());
        Assert.Equal(SoftwareProject.UniqueNameIndex, refused.ConstraintName);

        await using var fresh = fixture.CreateAppContext(_tenantId);
        Assert.Equal(1, await fresh.Projects.CountAsync(project => project.Name == "Tesorería"));
    }

    [Fact]
    public async Task The_service_turns_that_race_into_the_same_refusal_as_the_courteous_path()
    {
        await using var winner = fixture.CreateAppContext(_tenantId);
        await Service(winner, _tenantId).CreateAsync(new CreateProjectCommand("Caja", null), CancellationToken.None);

        await using var loser = fixture.CreateAppContext(_tenantId);
        var exception = await Assert.ThrowsAsync<ProjectNameTakenException>(
            () => Service(loser, _tenantId).CreateAsync(new CreateProjectCommand("Caja", null), CancellationToken.None));

        Assert.Equal("Caja", exception.Name);
    }

    [Fact]
    public async Task Another_tenant_neither_lists_nor_opens_this_tenants_project()
    {
        await using var owner = fixture.CreateAppContext(_tenantId);
        var mine = await Service(owner, _tenantId).CreateAsync(new CreateProjectCommand("Cobranzas", null), CancellationToken.None);

        Guid otherTenantId;
        await using (var admin = fixture.CreateAdminContext())
        {
            otherTenantId = await TenantGraph.CreateAsync(admin);
        }

        await using var intruder = fixture.CreateAppContext(otherTenantId);
        var service = Service(intruder, otherTenantId);

        var page = await service.SearchAsync(new ProjectFilter(), CancellationToken.None);
        Assert.DoesNotContain(page.Items, project => project.Id == mine.Id);

        // Not a comparison in code: for this session the row does not exist at all (HU-007 §3).
        await Assert.ThrowsAsync<ProjectNotFoundException>(() => service.GetAsync(mine.Id, CancellationToken.None));
    }

    [Fact]
    public async Task The_same_name_in_another_tenant_is_not_a_conflict()
    {
        await using var owner = fixture.CreateAppContext(_tenantId);
        await Service(owner, _tenantId).CreateAsync(new CreateProjectCommand("Cobranzas", null), CancellationToken.None);

        Guid otherTenantId;
        await using (var admin = fixture.CreateAdminContext())
        {
            otherTenantId = await TenantGraph.CreateAsync(admin);
        }

        await using var neighbour = fixture.CreateAppContext(otherTenantId);
        var created = await Service(neighbour, otherTenantId).CreateAsync(new CreateProjectCommand("Cobranzas", null), CancellationToken.None);

        // The index is per tenant: two clients may perfectly well both have a «Cobranzas».
        Assert.Equal("Cobranzas", created.Name);
    }

    private static async Task<Exception?> Commit(UnitOfWork unitOfWork)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            return null;
        }
        catch (UniqueConstraintViolationException exception)
        {
            return exception;
        }
    }

    private static ProjectService Service(SoftwareFactoryDbContext context, Guid tenantId) =>
        new(
            new ProjectRepository(context),
            new UnitOfWork(context),
            new FixedTenantContext(tenantId),
            NullLogger<ProjectService>.Instance);
}
