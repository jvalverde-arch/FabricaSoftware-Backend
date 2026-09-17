namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// Public contract of the project tree (HU-004). One entry point with two shapes — the children of a node, or the
/// branches a filter matches — and neither of them ever walks the project whole.
/// </summary>
public interface IProjectTreeService
{
    Task<ProjectTreeDto> GetAsync(ProjectTreeQuery query, CancellationToken cancellationToken);
}
