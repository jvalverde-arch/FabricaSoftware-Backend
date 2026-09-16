using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SoftwareFactory.Api.Contracts.Artifacts;

namespace SoftwareFactory.Api.Tests.Integration;

/// <summary>Contracts of HU-001 over HTTP: create, version, diff, list, delete guard and schema errors per field.</summary>
[Collection(ApiCollectionDefinition.Name)]
public sealed class ArtifactEndpointsTests(ApiFixture fixture)
{
    private const string Story = """
        {"as_a":"cajera","i_want":"registrar un pago","so_that":"la deuda baje","acceptance_criteria":["el saldo baja"]}
        """;

    [Fact]
    public async Task Creating_an_artifact_returns_it_with_its_first_version()
    {
        var (client, token, projectId) = await SignedInAsync();

        using var response = await PostAsync(client, token, projectId, "user_story", "Registrar pago", Story);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await Read<ArtifactDetailResponse>(response);
        Assert.Equal("user_story", created.Artifact.Type);
        Assert.Equal("draft", created.Artifact.State);
        Assert.Equal("project", created.Artifact.Level);
        Assert.Equal(1, created.Artifact.CurrentVersion);
        Assert.Equal(1, created.SchemaVersion);
        Assert.Equal("human", created.LastAuthor.Type);
        Assert.Equal("cajera", created.Content.GetProperty("as_a").GetString());
        Assert.Contains($"/api/artifacts/{created.Artifact.Id}", response.Headers.Location?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Content_that_breaks_the_schema_answers_422_with_the_field_that_is_wrong()
    {
        var (client, token, projectId) = await SignedInAsync();

        using var response = await PostAsync(client, token, projectId, "user_story", "Incompleta", """{"as_a":"cajera"}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await Read<ValidationProblemDetails>(response);
        Assert.Equal("El contenido no cumple el esquema de su tipo.", problem.Title);
        Assert.Contains(problem.Errors.Keys, key => key.Contains("soThat", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_type_outside_the_catalog_answers_422_naming_the_type()
    {
        var (client, token, projectId) = await SignedInAsync();

        using var response = await PostAsync(client, token, projectId, "dragon", "Nope", "{}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await Read<ValidationProblemDetails>(response);
        Assert.Contains("dragon", string.Join(' ', problem.Errors["type"]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Editing_produces_a_new_version_and_the_diff_shows_what_changed()
    {
        var (client, token, projectId) = await SignedInAsync();
        using var createdResponse = await PostAsync(client, token, projectId, "user_story", "Registrar pago", Story);
        var created = await Read<ArtifactDetailResponse>(createdResponse);

        using var updatedResponse = await PutAsync(
            client,
            token,
            $"/api/artifacts/{created.Artifact.Id}",
            """{"content":{"as_a":"cajera","i_want":"registrar un pago parcial","so_that":"la deuda baje","acceptance_criteria":["el saldo baja","queda recibo"]}}""");

        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        Assert.Equal(2, (await Read<ArtifactDetailResponse>(updatedResponse)).Artifact.CurrentVersion);

        using var versionsResponse = await client.GetWithTokenAsync($"/api/artifacts/{created.Artifact.Id}/versions", token);
        var versions = await Read<List<ArtifactVersionResponse>>(versionsResponse);
        Assert.Equal([1, 2], versions.Select(version => version.Number));

        using var diffResponse = await client.GetWithTokenAsync($"/api/artifacts/{created.Artifact.Id}/versions/1/diff/2", token);
        var diff = await Read<ArtifactDiffResponse>(diffResponse);
        Assert.Contains(diff.Changes, change => change.Path == "i_want" && change.Kind == "modified");
        Assert.Contains(diff.Changes, change => change.Path == "acceptance_criteria[1]" && change.Kind == "added");
    }

    [Fact]
    public async Task A_state_that_the_machine_does_not_allow_is_refused()
    {
        var (client, token, projectId) = await SignedInAsync();
        using var createdResponse = await PostAsync(client, token, projectId, "user_story", "Estados", Story);
        var created = await Read<ArtifactDetailResponse>(createdResponse);

        using var review = await PutAsync(client, token, $"/api/artifacts/{created.Artifact.Id}", """{"state":"in_review"}""");
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        Assert.Equal("in_review", (await Read<ArtifactDetailResponse>(review)).Artifact.State);

        using var frozen = await PutAsync(client, token, $"/api/artifacts/{created.Artifact.Id}", """{"state":"frozen"}""");
        Assert.Equal(HttpStatusCode.Conflict, frozen.StatusCode);
        Assert.Equal(
            "Un artefacto en «in_review» no puede pasar a «frozen».",
            (await Read<ProblemDetails>(frozen)).Detail);
    }

    [Fact]
    public async Task The_list_filters_and_pages()
    {
        var (client, token, projectId) = await SignedInAsync();
        using var first = await PostAsync(client, token, projectId, "user_story", "Pago con tarjeta", Story);
        using var second = await PostAsync(client, token, projectId, "screen", "Pantalla de caja", """{"purpose":"cobrar"}""");

        using var byType = await client.GetWithTokenAsync($"/api/projects/{projectId}/artifacts?type=screen", token);
        var screens = await Read<ArtifactPageResponse>(byType);
        Assert.All(screens.Items, artifact => Assert.Equal("screen", artifact.Type));
        Assert.Contains(screens.Items, artifact => artifact.Title == "Pantalla de caja");

        using var byTitle = await client.GetWithTokenAsync($"/api/projects/{projectId}/artifacts?title=tarjeta", token);
        Assert.Equal("Pago con tarjeta", Assert.Single((await Read<ArtifactPageResponse>(byTitle)).Items).Title);

        using var paged = await client.GetWithTokenAsync($"/api/projects/{projectId}/artifacts?take=1", token);
        var page = await Read<ArtifactPageResponse>(paged);
        Assert.Single(page.Items);
        Assert.True(page.Total >= 2);
    }

    [Fact]
    public async Task Deleting_answers_409_with_the_relations_that_block_it()
    {
        var (client, token, projectId) = await SignedInAsync();
        using var storyResponse = await PostAsync(client, token, projectId, "user_story", "Con prueba asociada", Story);
        var story = await Read<ArtifactDetailResponse>(storyResponse);
        using var testResponse = await PostAsync(client, token, projectId, "test_case", "Valida el pago", """{"kind":"e2e","steps":["pagar"],"expected_result":"saldo baja"}""");
        var test = await Read<ArtifactDetailResponse>(testResponse);

        await fixture.RelateAsync(test.Artifact.Id, story.Artifact.Id, "validates");

        using var blocked = await DeleteAsync(client, token, $"/api/artifacts/{story.Artifact.Id}");

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var problem = await Read<JsonElement>(blocked);
        Assert.Equal("No se puede eliminar el artefacto", problem.GetProperty("title").GetString());
        var relations = problem.GetProperty("relations").EnumerateArray().ToList();
        Assert.Equal("validates", Assert.Single(relations).GetProperty("type").GetString());
        Assert.Equal("Valida el pago", relations[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Deleting_without_relations_hides_the_artifact()
    {
        var (client, token, projectId) = await SignedInAsync();
        using var createdResponse = await PostAsync(client, token, projectId, "user_story", "Efímera", Story);
        var created = await Read<ArtifactDetailResponse>(createdResponse);

        using var deleted = await DeleteAsync(client, token, $"/api/artifacts/{created.Artifact.Id}");
        using var read = await client.GetWithTokenAsync($"/api/artifacts/{created.Artifact.Id}", token);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal("El artefacto no existe o no pertenece a tu organización.", (await Read<ProblemDetails>(read)).Detail);
    }

    [Fact]
    public async Task An_artifact_of_another_tenant_answers_404()
    {
        var (client, token, _) = await SignedInAsync();
        var foreign = await fixture.CreateForeignArtifactAsync();

        using var read = await client.GetWithTokenAsync($"/api/artifacts/{foreign}", token);
        using var versions = await client.GetWithTokenAsync($"/api/artifacts/{foreign}/versions", token);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, versions.StatusCode);
    }

    [Fact]
    public async Task Every_artifact_endpoint_needs_a_session()
    {
        using var client = fixture.CreateClient();
        var projectId = await fixture.LocalProjectIdAsync();

        using var create = await client.PostAsJsonAsync(new Uri($"/api/projects/{projectId}/artifacts", UriKind.Relative), new { type = "user_story", title = "x", content = new { } }, AuthClientExtensions.Json);
        using var list = await client.GetWithTokenAsync($"/api/projects/{projectId}/artifacts", accessToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
    }

    private async Task<(HttpClient Client, string Token, Guid ProjectId)> SignedInAsync()
    {
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(ApiFixture.AdminEmail, ApiFixture.AdminPassword);
        return (client, session.AccessToken, await fixture.LocalProjectIdAsync());
    }

    // Bodies travel as raw JSON: a JsonElement taken from a JsonDocument would be read after the document is
    // disposed, and the request would fail before reaching the Api.
    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token, Guid projectId, string type, string title, string content) =>
        SendAsync(
            client,
            token,
            HttpMethod.Post,
            $"/api/projects/{projectId}/artifacts",
            $$"""{"type":{{JsonSerializer.Serialize(type)}},"title":{{JsonSerializer.Serialize(title)}},"content":{{content}}}""");

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, string token, string path, string body) =>
        SendAsync(client, token, HttpMethod.Put, path, body);

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, string token, string path) =>
        SendAsync(client, token, HttpMethod.Delete, path, body: null);

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string token, HttpMethod method, string path, string? body)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
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
