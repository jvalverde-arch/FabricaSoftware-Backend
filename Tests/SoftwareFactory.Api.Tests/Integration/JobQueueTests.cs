using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SoftwareFactory.Api.Contracts.Jobs;

namespace SoftwareFactory.Api.Tests.Integration;

/// <summary>
/// Acceptance criterion of T-007: a probe job queued from the Api runs in the worker and its progress is visible
/// through the server-sent events stream — and the stream lets go when the client does.
/// </summary>
[Collection(ApiCollectionDefinition.Name)]
public sealed class JobQueueTests(ApiFixture fixture)
{
    [Fact]
    public async Task A_queued_job_runs_and_its_progress_can_be_followed_by_server_sent_events()
    {
        using var client = fixture.CreateClient();
        var session = await client.LoginAsync(ApiFixture.AdminEmail, ApiFixture.AdminPassword);

        // Long enough that the subscription always beats the run: with a fast job the worker could finish before the
        // client connects and there would be no progress event to see (the run would still be correct).
        var queued = await EnqueueAsync(client, session.AccessToken, """{"steps":3,"delayMs":600}""");
        Assert.Equal("pending", queued.State);

        var events = await ReadEventsAsync(client, session.AccessToken, queued.Id, TimeSpan.FromSeconds(30));

        Assert.Equal("completed", events[^1].Name);
        var final = events[^1].Job;
        Assert.Equal("succeeded", final.State);
        Assert.Equal(100, final.ProgressPercent);
        Assert.Equal(1, final.Attempts);
        Assert.Null(final.LastError);

        // The run reported its phases: either seen live or, if the client arrived late, kept on the finished row.
        var phases = events.Select(e => e.Job.Phase).Where(phase => phase is not null).Distinct().ToList();
        Assert.NotEmpty(phases);
        Assert.Contains("leyendo contexto", phases);
        Assert.All(events, e => Assert.Equal(queued.Id, e.Job.Id));
    }

    [Fact]
    public async Task A_job_that_keeps_failing_ends_as_failed_with_its_error()
    {
        using var client = fixture.CreateClient();
        var session = await client.LoginAsync(ApiFixture.AdminEmail, ApiFixture.AdminPassword);

        var queued = await EnqueueAsync(client, session.AccessToken, """{"steps":1,"fail":true}""");
        var events = await ReadEventsAsync(client, session.AccessToken, queued.Id, TimeSpan.FromSeconds(40));

        var final = events[^1].Job;
        Assert.Equal("failed", final.State);
        Assert.Equal("El job de prueba falló a propósito.", final.LastError);
        Assert.True(final.Attempts >= 1);
    }

    [Fact]
    public async Task The_stream_of_a_finished_job_sends_one_event_and_closes()
    {
        using var client = fixture.CreateClient();
        var session = await client.LoginAsync(ApiFixture.AdminEmail, ApiFixture.AdminPassword);
        var queued = await EnqueueAsync(client, session.AccessToken, """{"steps":1}""");
        await ReadEventsAsync(client, session.AccessToken, queued.Id, TimeSpan.FromSeconds(30));

        var events = await ReadEventsAsync(client, session.AccessToken, queued.Id, TimeSpan.FromSeconds(10));

        var (name, job) = Assert.Single(events);
        Assert.Equal("completed", name);
        Assert.Equal("succeeded", job.State);
    }

    [Fact]
    public async Task The_stream_stops_as_soon_as_the_client_disconnects()
    {
        using var client = fixture.CreateClient();
        var session = await client.LoginAsync(ApiFixture.AdminEmail, ApiFixture.AdminPassword);
        // A long run so the stream is still open when the client walks away.
        var queued = await EnqueueAsync(client, session.AccessToken, """{"steps":20,"delayMs":500}""");

        using var cancellation = new CancellationTokenSource();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/api/jobs/{queued.Id}/events", UriKind.Relative));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.AccessToken);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        await using var body = await response.Content.ReadAsStreamAsync(cancellation.Token);
        using var reader = new StreamReader(body);
        Assert.False(string.IsNullOrEmpty(await reader.ReadLineAsync(cancellation.Token)));

        // The client goes away mid-stream: the server must let go instead of holding the connection.
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            while (await reader.ReadLineAsync(CancellationToken.None) is not null)
            {
                cancellation.Token.ThrowIfCancellationRequested();
            }
        });

        // The host stays healthy and keeps serving: no connection left hanging behind.
        using var health = await client.GetAsync(new Uri("/health", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task A_run_of_another_tenant_does_not_exist_for_this_caller()
    {
        using var client = fixture.CreateClient();
        var session = await client.LoginAsync(ApiFixture.AdminEmail, ApiFixture.AdminPassword);
        var foreignJob = await fixture.QueueForeignJobAsync();

        using var read = await client.GetWithTokenAsync($"/api/jobs/{foreignJob}", session.AccessToken);
        using var stream = await client.GetWithTokenAsync($"/api/jobs/{foreignJob}/events", session.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, stream.StatusCode);
        var problem = await read.Content.ReadFromJsonAsync<ProblemDetails>(AuthClientExtensions.Json);
        Assert.Equal("La corrida no existe o no pertenece a tu organización.", problem!.Detail);
    }

    [Fact]
    public async Task Queueing_and_reading_require_a_session()
    {
        using var client = fixture.CreateClient();

        using var enqueue = await client.PostAsJsonAsync(new Uri("/api/jobs", UriKind.Relative), new { type = "probe" }, AuthClientExtensions.Json);
        using var read = await client.GetWithTokenAsync($"/api/jobs/{Guid.CreateVersion7()}", accessToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, enqueue.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
    }

    private static async Task<JobResponse> EnqueueAsync(HttpClient client, string accessToken, string payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/jobs", UriKind.Relative))
        {
            Content = JsonContent.Create(new { type = "probe", payload }, options: AuthClientExtensions.Json),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var job = await response.Content.ReadFromJsonAsync<JobResponse>(AuthClientExtensions.Json);
        Assert.NotNull(job);
        return job;
    }

    private static async Task<List<(string Name, JobResponse Job)>> ReadEventsAsync(
        HttpClient client,
        string accessToken,
        Guid jobId,
        TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/api/jobs/{jobId}/events", UriKind.Relative));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var body = await response.Content.ReadAsStreamAsync(cancellation.Token);
        using var reader = new StreamReader(body);

        List<(string Name, JobResponse Job)> events = [];
        string? name = null;

        while (await reader.ReadLineAsync(cancellation.Token) is { } line)
        {
            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                name = line["event: ".Length..];
            }
            else if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                var job = JsonSerializer.Deserialize<JobResponse>(line["data: ".Length..], AuthClientExtensions.Json);
                Assert.NotNull(job);
                events.Add((name ?? "message", job));
            }
        }

        Assert.NotEmpty(events);
        return events;
    }
}
