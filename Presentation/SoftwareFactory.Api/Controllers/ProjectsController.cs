using Microsoft.AspNetCore.Mvc;
using SoftwareFactory.Api.Contracts.Projects;
using SoftwareFactory.Application.Project.Contracts;

namespace SoftwareFactory.Api.Controllers;

/// <summary>
/// Projects of the tenant (HU-007). The surface the platform was missing: until now a project could only be born
/// inside a test fixture, which left the close of the sprint with nothing to demo on.
/// </summary>
[ApiController]
[Route("api/projects")]
[Produces("application/json")]
public sealed class ProjectsController(IProjectService projects) : ControllerBase
{
    private const string GetProjectRouteName = "GetProject";

    [HttpPost]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectResponse>> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var created = await projects.CreateAsync(new CreateProjectCommand(request.Name, request.Description), cancellationToken);

        return CreatedAtRoute(GetProjectRouteName, new { projectId = created.Id }, created.ToResponse());
    }

    [HttpGet]
    [ProducesResponseType<ProjectPageResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectPageResponse>> SearchAsync(
        [FromQuery] int skip,
        [FromQuery] int take,
        CancellationToken cancellationToken)
    {
        var page = await projects.SearchAsync(new ProjectFilter { Skip = skip, Take = take == 0 ? 50 : take }, cancellationToken);

        return Ok(page.ToResponse());
    }

    [HttpGet("{projectId:guid}", Name = GetProjectRouteName)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);

        return Ok(project.ToResponse());
    }
}
