using Microsoft.EntityFrameworkCore;
using SoftwareFactory.Domain.Project;

namespace SoftwareFactory.Infrastructure.Persistence.Repositories;

/// <summary>
/// Projects of the current tenant (HU-007). Row-level security does the filtering, so these queries only express
/// intent — there is no tenant predicate to forget here, which is the point of doing it in the database.
/// </summary>
public sealed class ProjectRepository(SoftwareFactoryDbContext context) : IProjectRepository
{
    public void Add(SoftwareProject project) => context.Projects.Add(project);

    public Task<SoftwareProject?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Projects.SingleOrDefaultAsync(project => project.Id == id, cancellationToken);

    public Task<bool> NameTakenAsync(string name, CancellationToken cancellationToken) =>
        context.Projects.AnyAsync(project => project.Name == name, cancellationToken);

    public async Task<IReadOnlyList<SoftwareProject>> SearchAsync(ProjectQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await context.Projects
            .AsNoTracking()
            .OrderBy(project => project.Name)
            .ThenBy(project => project.Id)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken) => context.Projects.CountAsync(cancellationToken);
}
