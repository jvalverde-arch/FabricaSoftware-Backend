using Microsoft.AspNetCore.Mvc;
using SoftwareFactory.Api.Contracts.Tree;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Api.Controllers;

/// <summary>
/// Read-optimised view of the project tree (HU-004). Thin like the rest: the hierarchy, the ordering and the two
/// modes live in the module's service.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class ProjectTreeController(IProjectTreeService tree) : ControllerBase
{
    /// <summary>
    /// Children of a node, or the branches a filter matches. <paramref name="node"/> is the route of the node as the
    /// server handed it out — ids joined by «/», empty for the root — and not just its id, because only the route
    /// says which ancestors the branch already went through.
    /// </summary>
    [HttpGet("projects/{projectId:guid}/tree")]
    [ProducesResponseType<TreeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TreeResponse>> GetAsync(
        Guid projectId,
        [FromQuery] string? node,
        [FromQuery] int level,
        [FromQuery] string? type,
        [FromQuery] string? state,
        [FromQuery] string? text,
        CancellationToken cancellationToken)
    {
        var query = new ProjectTreeQuery(projectId)
        {
            Node = node,
            Level = level,
            Filter = new ProjectTreeFilter { Type = type, State = state, Text = text },
        };

        var found = await tree.GetAsync(query, cancellationToken);

        return Ok(found.ToResponse());
    }
}
