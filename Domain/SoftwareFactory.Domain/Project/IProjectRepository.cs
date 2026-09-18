namespace SoftwareFactory.Domain.Project;

/// <summary>
/// Projects of the current tenant (HU-007). Row-level security does the tenant filtering: a project of another
/// tenant is not hidden by a comparison here, it simply is not there.
/// </summary>
public interface IProjectRepository
{
    void Add(SoftwareProject project);

    Task<SoftwareProject?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>True when the tenant already has a project with that name; the unique index is what enforces it.</summary>
    Task<bool> NameTakenAsync(string name, CancellationToken cancellationToken);

    Task<IReadOnlyList<SoftwareProject>> SearchAsync(ProjectQuery query, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);
}

/// <summary>Page of the project list (HU-007 §2); every list of the product is paginated.</summary>
public sealed record ProjectQuery
{
    public int Skip { get; init; }

    public int Take { get; init; } = 50;
}
