using Microsoft.Extensions.Logging.Abstractions;
using SoftwareFactory.Application.Common.Persistence;
using SoftwareFactory.Application.Project;
using SoftwareFactory.Application.Project.Contracts;
using SoftwareFactory.Application.Tests.Finops.Fakes;
using SoftwareFactory.Application.Tests.Platform.Fakes;
using SoftwareFactory.Application.Tests.Project.Fakes;
using SoftwareFactory.Domain.Project;

namespace SoftwareFactory.Application.Tests.Project;

/// <summary>
/// The public contract of the project module (HU-007). The one rule worth its own cases is the name: the previous
/// check is a courtesy and the unique index is the authority, so both paths have to end in the same answer.
/// </summary>
public sealed class ProjectServiceTests
{
    private static readonly Guid _tenantId = Guid.CreateVersion7();

    [Fact]
    public async Task A_project_is_created_active_and_trimmed()
    {
        var harness = new Harness();

        var created = await harness.Service.CreateAsync(new CreateProjectCommand("  Cobranzas  ", "  Cobrar lo que se debe  "), CancellationToken.None);

        Assert.Equal("Cobranzas", created.Name);
        Assert.Equal("Cobrar lo que se debe", created.Description);
        Assert.Equal(ProjectNames.Active, created.State);
        Assert.Equal(1, harness.UnitOfWork.Commits);
    }

    [Fact]
    public async Task A_project_without_a_name_is_refused_naming_the_field()
    {
        var harness = new Harness();

        var exception = await Assert.ThrowsAsync<ProjectValidationException>(
            () => harness.Service.CreateAsync(new CreateProjectCommand("   ", null), CancellationToken.None));

        Assert.Equal("name", exception.Field);
        Assert.Empty(harness.Projects.Projects);
    }

    [Fact]
    public async Task A_name_the_tenant_already_uses_is_refused_with_the_name()
    {
        var harness = new Harness();
        await harness.Service.CreateAsync(new CreateProjectCommand("Cobranzas", null), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ProjectNameTakenException>(
            () => harness.Service.CreateAsync(new CreateProjectCommand("Cobranzas", null), CancellationToken.None));

        Assert.Equal("Cobranzas", exception.Name);
        Assert.Single(harness.Projects.Projects);
    }

    [Fact]
    public async Task The_write_that_loses_the_race_gets_the_same_answer_as_the_one_that_asked_first()
    {
        var harness = new Harness();

        // Nobody has the name when the courtesy check runs, and the index refuses the write anyway: that is exactly
        // what a second caller sees when two creates interleave (estandar-backend.md §4).
        harness.UnitOfWork.RefusedByIndex = SoftwareProject.UniqueNameIndex;

        var exception = await Assert.ThrowsAsync<ProjectNameTakenException>(
            () => harness.Service.CreateAsync(new CreateProjectCommand("Cobranzas", null), CancellationToken.None));

        Assert.Equal("Cobranzas", exception.Name);
        Assert.Equal(0, harness.UnitOfWork.Commits);
    }

    [Fact]
    public async Task A_violation_of_another_index_is_not_dressed_up_as_a_name_conflict()
    {
        var harness = new Harness();

        // The day this table — or anything else travelling in the same SaveChanges — grows a second unique index,
        // a blind catch would answer «that name is taken» about a field nobody touched (estandar-backend.md §4).
        harness.UnitOfWork.RefusedByIndex = "ux_project_something_else";

        var exception = await Assert.ThrowsAsync<UniqueConstraintViolationException>(
            () => harness.Service.CreateAsync(new CreateProjectCommand("Cobranzas", null), CancellationToken.None));

        Assert.Equal("ux_project_something_else", exception.ConstraintName);
    }

    [Fact]
    public async Task The_list_comes_paginated_with_its_total()
    {
        var harness = new Harness();

        foreach (var name in new[] { "Cobranzas", "Caja", "Tesorería" })
        {
            await harness.Service.CreateAsync(new CreateProjectCommand(name, null), CancellationToken.None);
        }

        var page = await harness.Service.SearchAsync(new ProjectFilter { Skip = 1, Take = 1 }, CancellationToken.None);

        Assert.Equal(3, page.Total);
        Assert.Equal(1, page.Skip);
        Assert.Equal(1, page.Take);
        Assert.Equal("Cobranzas", Assert.Single(page.Items).Name);
    }

    [Fact]
    public async Task A_project_that_is_not_there_is_a_404_and_not_an_empty_answer()
    {
        var harness = new Harness();

        await Assert.ThrowsAsync<ProjectNotFoundException>(
            () => harness.Service.GetAsync(Guid.CreateVersion7(), CancellationToken.None));
    }

    private sealed class Harness
    {
        public Harness()
        {
            Service = new ProjectService(Projects, UnitOfWork, new FakeTenantContext(_tenantId), NullLogger<ProjectService>.Instance);
        }

        public ProjectService Service { get; }

        public FakeProjectRepository Projects { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; } = new();
    }
}
