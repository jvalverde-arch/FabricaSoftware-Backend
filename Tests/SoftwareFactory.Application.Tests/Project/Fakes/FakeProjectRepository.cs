using SoftwareFactory.Domain.Project;

namespace SoftwareFactory.Application.Tests.Project.Fakes;

/// <summary>In-memory projects. Row-level security is the database's job, so the fake keeps only what the service
/// reasons about; the isolation is proven against a real database in the integration tests.</summary>
internal sealed class FakeProjectRepository : IProjectRepository
{
    public List<SoftwareProject> Projects { get; } = [];

    public void Add(SoftwareProject project) => Projects.Add(project);

    public Task<SoftwareProject?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Projects.SingleOrDefault(project => project.Id == id));

    public Task<bool> NameTakenAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(Projects.Any(project => string.Equals(project.Name, name, StringComparison.Ordinal)));

    public Task<IReadOnlyList<SoftwareProject>> SearchAsync(ProjectQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Task.FromResult<IReadOnlyList<SoftwareProject>>(
            [.. Projects.OrderBy(project => project.Name, StringComparer.Ordinal).Skip(query.Skip).Take(query.Take)]);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken) => Task.FromResult(Projects.Count);
}
