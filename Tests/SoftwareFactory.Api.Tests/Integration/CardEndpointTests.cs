using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SoftwareFactory.Api.Contracts.Artifacts;
using SoftwareFactory.Api.Contracts.Cards;

namespace SoftwareFactory.Api.Tests.Integration;

/// <summary>
/// The contract of the artifact card (HU-005) over HTTP: one call brings what the page draws, and the schema that
/// travels is the one of the version being shown.
/// </summary>
[Collection(ApiCollectionDefinition.Name)]
public sealed class CardEndpointTests(ApiFixture fixture)
{
    private const string Module = """{"name":"Cobranzas","purpose":"Cobrar lo que se debe"}""";

    private const string Story = """
        {"as_a":"cajera","i_want":"registrar un pago","so_that":"la deuda baje","acceptance_criteria":["el saldo baja"]}
        """;

    [Fact]
    public async Task The_card_answers_the_artifact_its_schema_and_its_relations_in_one_call()
    {
        var (client, token, projectId) = await SignedInAsync();
        var module = await ArtifactAsync(client, token, projectId, "module", $"Cobranzas {Guid.NewGuid():N}", Module);
        var story = await ArtifactAsync(client, token, projectId, "user_story", $"Registrar pago {Guid.NewGuid():N}", Story);
        await RelateAsync(client, token, story.Id, module.Id, "belongs_to");

        using var response = await SendAsync(client, token, $"/api/artifacts/{story.Id}/card");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var card = await Read<ArtifactCardResponse>(response);

        Assert.Equal(story.Id, card.Artifact.Id);
        Assert.Equal(1, card.Version);
        Assert.Equal("user_story", card.Schema.Type);

        // The schema travels as JSON, not as a string carrying JSON: the web parses nothing twice.
        Assert.Equal(JsonValueKind.Object, card.Schema.Json.ValueKind);
        Assert.True(card.Schema.Json.TryGetProperty("properties", out _));
        Assert.Equal(JsonValueKind.Object, card.Content.ValueKind);

        var upstream = Assert.Single(card.Upstream);
        Assert.Equal("belongs_to", upstream.Type);
        var target = Assert.Single(upstream.Items);
        Assert.Equal(module.Id, target.ArtifactId);
        Assert.False(target.CrossesProject);
        Assert.Single(card.Versions);
    }

    [Fact]
    public async Task Asking_for_an_older_version_brings_that_versions_content()
    {
        var (client, token, projectId) = await SignedInAsync();
        var story = await ArtifactAsync(client, token, projectId, "user_story", $"Registrar pago {Guid.NewGuid():N}", Story);

        using (var edited = await SendAsync(
            client,
            token,
            $"/api/artifacts/{story.Id}",
            HttpMethod.Put,
            $$"""{"title":"Registrar pago con vuelto","content":{{Story.Replace("la deuda baje", "la deuda baje y haya vuelto", StringComparison.Ordinal)}}}"""))
        {
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        }

        using var current = await SendAsync(client, token, $"/api/artifacts/{story.Id}/card");
        using var older = await SendAsync(client, token, $"/api/artifacts/{story.Id}/card?version=1");

        var now = await Read<ArtifactCardResponse>(current);
        var before = await Read<ArtifactCardResponse>(older);

        Assert.Equal(2, now.Version);
        Assert.Equal(1, before.Version);
        Assert.Equal(2, now.Versions.Count);

        // Each version answers with the schema it was written against, which is the only way an old card is honest.
        Assert.Equal(now.Schema.Type, before.Schema.Type);
        Assert.Equal(before.SchemaVersion, before.Schema.Version);
        Assert.Equal(now.SchemaVersion, now.Schema.Version);
        Assert.Contains("vuelto", now.Content.GetProperty("so_that").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("vuelto", before.Content.GetProperty("so_that").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_card_of_an_artifact_that_is_not_there_answers_404()
    {
        var (client, token, _) = await SignedInAsync();

        using var response = await SendAsync(client, token, $"/api/artifacts/{Guid.CreateVersion7()}/card");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_endpoint_travels_in_the_published_openapi_document()
    {
        var client = fixture.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/artifacts/{artifactId}/card", out var card));
        var parameters = card.GetProperty("get").GetProperty("parameters").EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString())
            .ToList();

        Assert.Equal<string?[]>(["artifactId", "version"], [.. parameters]);
    }

    private async Task<(HttpClient Client, string Token, Guid ProjectId)> SignedInAsync()
    {
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(ApiFixture.AdminEmail, ApiFixture.AdminPassword);
        return (client, session.AccessToken, await fixture.LocalProjectIdAsync());
    }

    private static async Task<ArtifactResponse> ArtifactAsync(HttpClient client, string token, Guid projectId, string type, string title, string content)
    {
        using var response = await SendAsync(
            client,
            token,
            $"/api/projects/{projectId}/artifacts",
            HttpMethod.Post,
            $$"""{"type":{{JsonSerializer.Serialize(type)}},"title":{{JsonSerializer.Serialize(title)}},"content":{{content}}}""");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Read<ArtifactDetailResponse>(response)).Artifact;
    }

    private static async Task RelateAsync(HttpClient client, string token, Guid sourceId, Guid targetId, string type)
    {
        using var response = await SendAsync(
            client,
            token,
            $"/api/artifacts/{sourceId}/relations",
            HttpMethod.Post,
            $$"""{"targetId":"{{targetId}}","type":{{JsonSerializer.Serialize(type)}}}""");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string token, string path, HttpMethod? method = null, string? body = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, new Uri(path, UriKind.Relative));
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
