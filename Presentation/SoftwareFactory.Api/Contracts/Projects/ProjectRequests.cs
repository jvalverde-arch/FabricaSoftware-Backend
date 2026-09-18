using SoftwareFactory.Application.Project.Contracts;

namespace SoftwareFactory.Api.Contracts.Projects;

/// <summary>Creates a project (HU-007 §1). No tenant here: it comes from the token, never from the body.</summary>
public sealed record CreateProjectRequest(string Name, string? Description);

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    string State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ProjectPageResponse(IReadOnlyList<ProjectResponse> Items, int Total, int Skip, int Take);

/// <summary>Turns the project contracts of the module into the wire shape.</summary>
public static class ProjectMapping
{
    public static ProjectResponse ToResponse(this ProjectDto project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return new ProjectResponse(
            project.Id,
            project.Name,
            project.Description,
            project.State,
            project.CreatedAt,
            project.UpdatedAt);
    }

    public static ProjectPageResponse ToResponse(this ProjectPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return new ProjectPageResponse([.. page.Items.Select(ToResponse)], page.Total, page.Skip, page.Take);
    }
}
