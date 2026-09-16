using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using SoftwareFactory.Api.Contracts.Jobs;
using SoftwareFactory.Api.Jobs;
using SoftwareFactory.Api.Resources;
using SoftwareFactory.Application.Platform.Contracts;

namespace SoftwareFactory.Api.Controllers;

/// <summary>
/// Queue of agent runs (T-007): enqueue, read state, and follow progress by server-sent events. The tenant always
/// comes from the token, so a run of another tenant simply does not exist for this caller.
/// </summary>
[ApiController]
[Route("api/jobs")]
[Produces("application/json")]
public sealed class JobsController(
    IJobEnqueuer enqueuer,
    IJobReader jobs,
    JobEventWriter stream,
    IStringLocalizer<ApiMessages> messages) : ControllerBase
{
    private const string GetRouteName = "GetJob";

    [HttpPost]
    [ProducesResponseType<JobResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<JobResponse>> EnqueueAsync(EnqueueJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = await enqueuer.EnqueueAsync(request.Type, request.Payload ?? "{}", cancellationToken);
        var snapshot = await jobs.GetAsync(id, cancellationToken);

        return snapshot is null
            ? Problem(statusCode: StatusCodes.Status500InternalServerError)
            // By route name: controllers trim the «Async» suffix from action names, so AcceptedAtAction would not resolve.
            : AcceptedAtRoute(GetRouteName, new { id }, JobResponse.From(snapshot));
    }

    [HttpGet("{id:guid}", Name = GetRouteName)]
    [ProducesResponseType<JobResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await jobs.GetAsync(id, cancellationToken);

        return snapshot is null ? NotFoundProblem() : Ok(JobResponse.From(snapshot));
    }

    /// <summary>Progress of a run as server-sent events; the connection closes when the run ends or the client leaves.</summary>
    [HttpGet("{id:guid}/events")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StreamAsync(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await jobs.GetAsync(id, cancellationToken);

        if (snapshot is null)
        {
            return NotFoundProblem();
        }

        await stream.WriteAsync(Response, snapshot, cancellationToken);
        return new EmptyResult();
    }

    private ObjectResult NotFoundProblem() =>
        Problem(detail: messages["JobNotFound"], statusCode: StatusCodes.Status404NotFound, title: messages["Status404Title"]);
}
