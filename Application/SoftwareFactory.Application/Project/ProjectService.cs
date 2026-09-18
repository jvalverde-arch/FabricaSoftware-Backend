using Microsoft.Extensions.Logging;
using SoftwareFactory.Application.Common.Persistence;
using SoftwareFactory.Application.Common.Tenancy;
using SoftwareFactory.Application.Project.Contracts;
using SoftwareFactory.Domain.Project;

namespace SoftwareFactory.Application.Project;

/// <summary>
/// The only way in and out of projects (HU-007). Deliberately small: the portfolio of S3 will grow around this, and
/// what lives here is what the close of S1 needs to be executable — create one, list them, open one.
/// </summary>
public sealed class ProjectService(
    IProjectRepository projects,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    ILogger<ProjectService> logger) : IProjectService
{
    private const int MaxTake = 200;

    public async Task<ProjectDto> CreateAsync(CreateProjectCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tenantId = RequireTenant();
        var name = (command.Name ?? string.Empty).Trim();

        if (name.Length == 0)
        {
            throw new ProjectValidationException("name", "El proyecto necesita un nombre.");
        }

        // Courtesy: asking first is what lets the answer name the project that is already there. The authority is
        // the unique index, and it is checked below — between this read and that write anybody can win the race
        // (estandar-backend.md §4).
        if (await projects.NameTakenAsync(name, cancellationToken).ConfigureAwait(false))
        {
            throw new ProjectNameTakenException(name);
        }

        var project = new SoftwareProject(tenantId, name, command.Description);
        projects.Add(project);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (UniqueConstraintViolationException exception)
            when (string.Equals(exception.ConstraintName, SoftwareProject.UniqueNameIndex, StringComparison.Ordinal))
        {
            // Discriminated by index name, and anything else is left to travel (estandar-backend.md §4): a blind
            // catch works while the table has one unique index and, when the second arrives, starts answering «that
            // name is taken» about a field nobody touched.
            logger.ProjectNameRaceLost(name);
            throw new ProjectNameTakenException(name, exception);
        }

        logger.ProjectCreated(project.Id, name);
        return Map(project);
    }

    public async Task<ProjectPage> SearchAsync(ProjectFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        RequireTenant();

        var take = filter.Take is > 0 and <= MaxTake ? filter.Take : MaxTake;
        var query = new ProjectQuery { Skip = Math.Max(filter.Skip, 0), Take = take };

        var items = await projects.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        var total = await projects.CountAsync(cancellationToken).ConfigureAwait(false);

        return new ProjectPage([.. items.Select(Map)], total, query.Skip, query.Take);
    }

    public async Task<ProjectDto> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        RequireTenant();

        // No tenant comparison here on purpose (HU-007 §3): for a session of another tenant the row does not exist,
        // because row-level security never returns it. A check in code would be a second, weaker copy of that rule.
        var project = await projects.GetAsync(projectId, cancellationToken).ConfigureAwait(false)
            ?? throw new ProjectNotFoundException(projectId);

        return Map(project);
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new InvalidOperationException("A project needs a tenant in context.");

    private static ProjectDto Map(SoftwareProject project) => new(
        project.Id,
        project.Name,
        project.Description,
        project.State == ProjectState.Archived ? ProjectNames.Archived : ProjectNames.Active,
        project.CreatedAt,
        project.UpdatedAt);
}
