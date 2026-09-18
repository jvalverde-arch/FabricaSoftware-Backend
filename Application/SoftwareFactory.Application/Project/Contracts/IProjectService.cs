namespace SoftwareFactory.Application.Project.Contracts;

/// <summary>
/// Public contract of the project module (HU-007). Minimal on purpose: the full portfolio is S3, and this is what
/// the close of S1 needs to be executable at all.
/// </summary>
public interface IProjectService
{
    Task<ProjectDto> CreateAsync(CreateProjectCommand command, CancellationToken cancellationToken);

    Task<ProjectPage> SearchAsync(ProjectFilter filter, CancellationToken cancellationToken);

    Task<ProjectDto> GetAsync(Guid projectId, CancellationToken cancellationToken);
}
