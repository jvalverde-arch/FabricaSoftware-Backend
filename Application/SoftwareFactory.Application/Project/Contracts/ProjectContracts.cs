namespace SoftwareFactory.Application.Project.Contracts;

/// <summary>
/// Creates a project (HU-007 §1). The tenant is not here on purpose: it comes from the token and never from the
/// body, the same rule the whole platform follows.
/// </summary>
public sealed record CreateProjectCommand(string Name, string? Description);

/// <summary>Filters of the project list; only paging for now (HU-007 §2).</summary>
public sealed record ProjectFilter
{
    public int Skip { get; init; }

    public int Take { get; init; } = 50;
}

/// <summary>A project as the rest of the platform sees it.</summary>
public sealed record ProjectDto(
    Guid Id,
    string Name,
    string? Description,
    string State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ProjectPage(IReadOnlyList<ProjectDto> Items, int Total, int Skip, int Take);

/// <summary>Wire names of the project states; the same snake_case the database stores.</summary>
public static class ProjectNames
{
    public const string Active = "active";
    public const string Archived = "archived";
}
