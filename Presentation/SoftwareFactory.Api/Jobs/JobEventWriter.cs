using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using SoftwareFactory.Api.Contracts.Jobs;
using SoftwareFactory.Application.Platform.Contracts;

namespace SoftwareFactory.Api.Jobs;

/// <summary>
/// Server-sent events of a run (T-007). Writes an event whenever the row changes, a comment as heartbeat when it does
/// not, and closes as soon as the run finishes, the ceiling is reached, or the client goes away — a stream nobody
/// reads is a connection and a database poll wasted.
/// </summary>
public sealed class JobEventWriter(IJobReader jobs, IOptions<JobStreamOptions> options, TimeProvider clock, ILogger<JobEventWriter> logger)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(HttpResponse response, JobSnapshot initial, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(initial);

        var settings = options.Value;

        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache, no-store";
        response.Headers.Connection = "keep-alive";
        // Nginx (and the SPA container of the self-hosted package) buffers by default, which would hold the events back.
        response.Headers["X-Accel-Buffering"] = "no";
        response.HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var deadline = clock.GetUtcNow().Add(settings.MaxDuration);
        var current = initial;
        var lastWriteAt = clock.GetUtcNow();

        // A client that arrives late gets the outcome in one event and the stream closes right away.
        await SendAsync(response, current.IsFinished ? "completed" : "state", current, cancellationToken).ConfigureAwait(false);

        while (!current.IsFinished)
        {
            if (clock.GetUtcNow() >= deadline)
            {
                await SendCommentAsync(response, "timeout", cancellationToken).ConfigureAwait(false);
                logger.StreamTimedOut(current.Id, settings.MaxDuration);
                return;
            }

            try
            {
                await Task.Delay(settings.PollInterval, clock, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The client disconnected or the host is shutting down; nothing else to write.
                logger.StreamClosedByClient(current.Id);
                return;
            }

            var latest = await jobs.GetAsync(current.Id, cancellationToken).ConfigureAwait(false);

            if (latest is null)
            {
                return;
            }

            if (HasChanged(current, latest))
            {
                current = latest;
                await SendAsync(response, current.IsFinished ? "completed" : "progress", current, cancellationToken).ConfigureAwait(false);
                lastWriteAt = clock.GetUtcNow();
                continue;
            }

            if (clock.GetUtcNow() - lastWriteAt >= settings.Heartbeat)
            {
                await SendCommentAsync(response, "ping", cancellationToken).ConfigureAwait(false);
                lastWriteAt = clock.GetUtcNow();
            }
        }
    }

    private static bool HasChanged(JobSnapshot current, JobSnapshot latest) =>
        latest.State != current.State
        || latest.Phase != current.Phase
        || latest.ProgressPercent != current.ProgressPercent
        || latest.Attempts != current.Attempts;

    private static async Task SendAsync(HttpResponse response, string eventName, JobSnapshot snapshot, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(JobResponse.From(snapshot), _json);
        await response.WriteAsync(
            string.Create(CultureInfo.InvariantCulture, $"event: {eventName}\ndata: {payload}\n\n"),
            cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task SendCommentAsync(HttpResponse response, string comment, CancellationToken cancellationToken)
    {
        await response.WriteAsync($": {comment}\n\n", cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
