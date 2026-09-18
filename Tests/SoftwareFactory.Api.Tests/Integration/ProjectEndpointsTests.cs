using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SoftwareFactory.Api.Contracts.Projects;

namespace SoftwareFactory.Api.Tests.Integration;

/// <summary>
/// The contract of HU-007 over HTTP: the surface the platform was missing. The name is the only rule with teeth,
/// and both ways of breaking it — asking twice, and asking at the same time — have to end in the same answer.
/// </summary>
[Collection(ApiCollectionDefinition.Name)]
public sealed class ProjectEndpointsTests(ApiFixture fixture)
{
    [Fact]
    public async Task A_project_is_created_and_then_listed_and_opened()
    {
        var (client, token) = await SignedInAsync();
        var name = $"Cobranzas {Guid.NewGuid():N}";

        using var response = await PostAsync(client, token, name, "Cobrar lo que se debe");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await Read<ProjectResponse>(response);
        Assert.Equal(name, created.Name);
        Assert.Equal("active", created.State);
        Assert.Equal($"/api/projects/{created.Id}", response.Headers.Location?.AbsolutePath);

        using var listed = await SendAsync(client, token, HttpMethod.Get, "/api/projects?take=200");
        var page = await Read<ProjectPageResponse>(listed);
        Assert.Contains(page.Items, project => project.Id == created.Id);
        Assert.True(page.Total >= 1);

        using var opened = await SendAsync(client, token, HttpMethod.Get, $"/api/projects/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Equal(created.Id, (await Read<ProjectResponse>(opened)).Id);
    }

    [Fact]
    public async Task A_name_the_tenant_already_uses_answers_409_naming_it()
    {
        var (client, token) = await SignedInAsync();
        var name = $"Caja {Guid.NewGuid():N}";

        using (var first = await PostAsync(client, token, name, null))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        using var second = await PostAsync(client, token, name, null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await Read<ProblemDetails>(second);
        Assert.Contains(name, problem.Detail, StringComparison.Ordinal);
        Assert.Equal(name, problem.Extensions["name"]?.ToString());
    }

    [Fact]
    public async Task Two_simultaneous_creates_of_the_same_name_leave_one_project_and_answer_409_not_500()
    {
        var (client, token) = await SignedInAsync();
        var name = $"Tesorería {Guid.NewGuid():N}";

        var both = await Task.WhenAll(PostAsync(client, token, name, null), PostAsync(client, token, name, null));

        try
        {
            var codes = both.Select(response => response.StatusCode).ToList();
            Assert.Single(codes, HttpStatusCode.Created);
            Assert.Single(codes, HttpStatusCode.Conflict);
            Assert.DoesNotContain(HttpStatusCode.InternalServerError, codes);

            using var listed = await SendAsync(client, token, HttpMethod.Get, "/api/projects?take=200");
            var page = await Read<ProjectPageResponse>(listed);
            Assert.Single(page.Items, project => project.Name == name);
        }
        finally
        {
            foreach (var response in both)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task A_blank_name_answers_422_naming_the_field()
    {
        var (client, token) = await SignedInAsync();

        using var response = await PostAsync(client, token, "   ", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await Read<ValidationProblemDetails>(response);
        Assert.True(problem.Errors.ContainsKey("name"));
    }

    [Fact]
    public async Task A_project_of_another_tenant_is_not_listed_and_answers_404()
    {
        var (client, token) = await SignedInAsync();
        var foreign = await fixture.CreateForeignProjectAsync($"Ajeno {Guid.NewGuid():N}");

        using var listed = await SendAsync(client, token, HttpMethod.Get, "/api/projects?take=200");
        var page = await Read<ProjectPageResponse>(listed);
        Assert.DoesNotContain(page.Items, project => project.Id == foreign);

        using var opened = await SendAsync(client, token, HttpMethod.Get, $"/api/projects/{foreign}");
        Assert.Equal(HttpStatusCode.NotFound, opened.StatusCode);
    }

    [Fact]
    public async Task The_endpoints_travel_in_the_published_openapi_document()
    {
        var client = fixture.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        // The frontend of this same HU generates its client from here (estandar-ui.md §7.5).
        Assert.True(paths.TryGetProperty("/api/projects", out var collection));
        Assert.True(collection.TryGetProperty("post", out _));
        Assert.True(collection.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/api/projects/{projectId}", out _));
    }

    private async Task<(HttpClient Client, string Token)> SignedInAsync()
    {
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(ApiFixture.AdminEmail, ApiFixture.AdminPassword);
        return (client, session.AccessToken);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string name, string? description) =>
        SendAsync(
            client,
            token,
            HttpMethod.Post,
            "/api/projects",
            $$"""{"name":{{JsonSerializer.Serialize(name)}},"description":{{JsonSerializer.Serialize(description)}}}""");

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string token, HttpMethod method, string path, string? body = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(AuthClientExtensions.Json);
        Assert.NotNull(value);
        return value;
    }
}
