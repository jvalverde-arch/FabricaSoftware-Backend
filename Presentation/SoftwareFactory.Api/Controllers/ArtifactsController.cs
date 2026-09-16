using Microsoft.AspNetCore.Mvc;
using SoftwareFactory.Api.Contracts.Artifacts;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Api.Controllers;

/// <summary>
/// Artifacts of the traceability model (HU-001). Thin on purpose: every rule — schema validation, versioning, audit,
/// the delete guard — lives in the module's service, which is also what the agents call.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class ArtifactsController(IArtifactService artifacts) : ControllerBase
{
    private const string GetRouteName = "GetArtifact";

    [HttpPost("projects/{projectId:guid}/artifacts")]
    [ProducesResponseType<ArtifactDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ArtifactDetailResponse>> CreateAsync(Guid projectId, CreateArtifactRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new CreateArtifactCommand(projectId, request.Type, request.Title, request.Content.GetRawText())
        {
            Level = request.Level ?? ArtifactNames.ProjectLevel,
        };

        var created = await artifacts.CreateAsync(command, cancellationToken);

        return CreatedAtRoute(GetRouteName, new { artifactId = created.Artifact.Id }, created.ToResponse());
    }

    [HttpGet("artifacts/{artifactId:guid}", Name = GetRouteName)]
    [ProducesResponseType<ArtifactDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArtifactDetailResponse>> GetAsync(Guid artifactId, [FromQuery] bool forEditing, CancellationToken cancellationToken)
    {
        // `forEditing` brings the content to the current schema of its type; without it the content comes back
        // exactly as it was written, which is what the version history and the diff need (HU-001).
        var detail = forEditing
            ? await artifacts.GetForEditingAsync(artifactId, cancellationToken)
            : await artifacts.GetAsync(artifactId, cancellationToken);

        return Ok(detail.ToResponse());
    }

    [HttpPut("artifacts/{artifactId:guid}")]
    [ProducesResponseType<ArtifactDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ArtifactDetailResponse>> UpdateAsync(Guid artifactId, UpdateArtifactRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new UpdateArtifactCommand(artifactId, request.Title, request.Content?.GetRawText())
        {
            State = request.State,
        };

        var updated = await artifacts.UpdateAsync(command, cancellationToken);

        return Ok(updated.ToResponse());
    }

    [HttpGet("projects/{projectId:guid}/artifacts")]
    [ProducesResponseType<ArtifactPageResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ArtifactPageResponse>> SearchAsync(
        Guid projectId,
        [FromQuery] string? type,
        [FromQuery] string? state,
        [FromQuery] Guid? moduleId,
        [FromQuery] int? minScore,
        [FromQuery] int? maxScore,
        [FromQuery] string? title,
        [FromQuery] int skip,
        [FromQuery] int? take,
        CancellationToken cancellationToken)
    {
        var filter = new ArtifactFilter(projectId)
        {
            Type = type,
            State = state,
            ModuleId = moduleId,
            MinScore = minScore,
            MaxScore = maxScore,
            Title = title,
            Skip = Math.Max(skip, 0),
            Take = Math.Clamp(take ?? 50, 1, 200),
        };

        var page = await artifacts.SearchAsync(filter, cancellationToken);

        return Ok(page.ToResponse());
    }

    [HttpGet("artifacts/{artifactId:guid}/versions")]
    [ProducesResponseType<IReadOnlyList<ArtifactVersionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ArtifactVersionResponse>>> GetVersionsAsync(Guid artifactId, CancellationToken cancellationToken)
    {
        var versions = await artifacts.GetVersionsAsync(artifactId, cancellationToken);

        return Ok(versions.Select(version => version.ToResponse()).ToList());
    }

    [HttpGet("artifacts/{artifactId:guid}/versions/{from:int}/diff/{to:int}")]
    [ProducesResponseType<ArtifactDiffResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArtifactDiffResponse>> GetDiffAsync(Guid artifactId, int from, int to, CancellationToken cancellationToken)
    {
        var diff = await artifacts.GetDiffAsync(artifactId, from, to, cancellationToken);

        return Ok(diff.ToResponse());
    }

    [HttpPut("artifacts/{artifactId:guid}/score")]
    [ProducesResponseType<ArtifactResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ArtifactResponse>> SetScoreAsync(Guid artifactId, SetArtifactScoreRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var artifact = await artifacts.SetScoreAsync(artifactId, request.Score, cancellationToken);

        return Ok(artifact.ToResponse());
    }

    [HttpDelete("artifacts/{artifactId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(Guid artifactId, CancellationToken cancellationToken)
    {
        await artifacts.DeleteAsync(artifactId, cancellationToken);

        return NoContent();
    }
}
