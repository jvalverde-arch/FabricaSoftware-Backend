using Microsoft.AspNetCore.Mvc;
using SoftwareFactory.Api.Contracts.Decisions;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Api.Controllers;

/// <summary>
/// The decision log (HU-003). Thin like the rest: what kind of entry this is, who may close it and whether the caller
/// qualifies are decided by the module's service, which the agents call through the same door.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class DecisionsController(IDecisionService decisions) : ControllerBase
{
    private const string GetDecisionsRouteName = "GetProjectDecisions";

    [HttpPost("projects/{projectId:guid}/decisions")]
    [ProducesResponseType<DecisionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DecisionResponse>> RecordAsync(Guid projectId, RecordDecisionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var recorded = await decisions.RecordAsync(
            new RecordDecisionCommand(projectId, request.ArtifactIds, request.Justification),
            cancellationToken);

        return CreatedAtRoute(GetDecisionsRouteName, new { projectId }, recorded.ToResponse());
    }

    [HttpGet("projects/{projectId:guid}/decisions", Name = GetDecisionsRouteName)]
    [ProducesResponseType<DecisionPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DecisionPageResponse>> SearchAsync(
        Guid projectId,
        [FromQuery(Name = "pending_role")] string? pendingRole,
        [FromQuery(Name = "artifact_id")] Guid? artifactId,
        [FromQuery] int skip,
        [FromQuery] int take,
        CancellationToken cancellationToken)
    {
        var filter = new DecisionFilter(projectId)
        {
            PendingRole = pendingRole,
            ArtifactId = artifactId,
            Skip = skip,
            Take = take == 0 ? 50 : take,
        };

        var page = await decisions.SearchAsync(filter, cancellationToken);

        return Ok(page.ToResponse());
    }

    [HttpPost("decisions/{decisionId:guid}/ratify")]
    [ProducesResponseType<DecisionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DecisionResponse>> RatifyAsync(Guid decisionId, CloseDecisionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ratification = await decisions.RatifyAsync(new CloseDecisionCommand(decisionId, request.Justification), cancellationToken);

        return Ok(ratification.ToResponse());
    }

    [HttpPost("decisions/{decisionId:guid}/revert")]
    [ProducesResponseType<DecisionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DecisionResponse>> RevertAsync(Guid decisionId, CloseDecisionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reversion = await decisions.RevertAsync(new CloseDecisionCommand(decisionId, request.Justification), cancellationToken);

        return Ok(reversion.ToResponse());
    }

    /// <summary>The competence map in force, so the artifact card can say who may ratify before anybody tries.</summary>
    [HttpGet("config/artifact-type-roles")]
    [ProducesResponseType<IReadOnlyList<ArtifactTypeRoleResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ArtifactTypeRoleResponse>>> GetCompetenceMapAsync(CancellationToken cancellationToken)
    {
        var map = await decisions.GetCompetenceMapAsync(cancellationToken);

        return Ok(map.Select(entry => entry.ToResponse()).ToList());
    }
}
