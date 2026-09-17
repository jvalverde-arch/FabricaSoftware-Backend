using Microsoft.AspNetCore.Mvc;
using SoftwareFactory.Api.Contracts.Relations;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Api.Controllers;

/// <summary>
/// Relations between artifacts (HU-002). Thin like the artifacts controller: the compatibility matrix, the
/// cross-project rule and the audit trail live in the module's service, which the agents call too.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class RelationsController(IRelationService relations) : ControllerBase
{
    private const string GetRelationsRouteName = "GetArtifactRelations";

    [HttpPost("artifacts/{artifactId:guid}/relations")]
    [ProducesResponseType<RelationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RelationResponse>> CreateAsync(Guid artifactId, CreateRelationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new CreateRelationCommand(artifactId, request.TargetId, request.Type)
        {
            Metadata = request.Metadata?.GetRawText(),
        };

        var created = await relations.CreateAsync(command, cancellationToken);

        return CreatedAtRoute(GetRelationsRouteName, new { artifactId }, created.ToResponse());
    }

    [HttpGet("artifacts/{artifactId:guid}/relations", Name = GetRelationsRouteName)]
    [ProducesResponseType<IReadOnlyList<RelationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RelationResponse>>> GetAsync(Guid artifactId, CancellationToken cancellationToken)
    {
        var found = await relations.GetRelationsAsync(artifactId, cancellationToken);

        return Ok(found.Select(relation => relation.ToResponse()).ToList());
    }

    [HttpDelete("artifacts/{artifactId:guid}/relations/{relationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid artifactId, Guid relationId, CancellationToken cancellationToken)
    {
        await relations.DeleteAsync(artifactId, relationId, cancellationToken);

        return NoContent();
    }

    [HttpGet("artifacts/{artifactId:guid}/neighborhood")]
    [ProducesResponseType<NeighborhoodResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<NeighborhoodResponse>> GetNeighborhoodAsync(Guid artifactId, [FromQuery] int levels, CancellationToken cancellationToken)
    {
        var neighborhood = await relations.GetNeighborhoodAsync(artifactId, levels == 0 ? 1 : levels, cancellationToken);

        return Ok(neighborhood.ToResponse());
    }

    [HttpGet("projects/{projectId:guid}/orphans")]
    [ProducesResponseType<IReadOnlyList<OrphanResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyList<OrphanResponse>>> GetOrphansAsync(
        Guid projectId,
        [FromQuery] string type,
        [FromQuery] string missing,
        CancellationToken cancellationToken)
    {
        var orphans = await relations.GetOrphansAsync(projectId, type, missing, cancellationToken);

        return Ok(orphans.Select(orphan => orphan.ToResponse()).ToList());
    }
}
