using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SoftwareFactory.Api.Contracts.Artifacts;
using SoftwareFactory.Api.Contracts.Relations;

namespace SoftwareFactory.Api.Tests.Integration;

/// <summary>Contracts of HU-002 over HTTP: the matrix, the cross-project rule, the neighborhood and the orphans.</summary>
[Collection(ApiCollectionDefinition.Name)]
public sealed class RelationEndpointsTests(ApiFixture fixture)
{
    private const string Story = """
        {"as_a":"cajera","i_want":"registrar un pago","so_that":"la deuda baje","acceptance_criteria":["el saldo baja"]}
        """;

    private const string Module = """{"name":"Cobranzas","purpose":"Cobrar lo que se debe"}""";

    private const string TestCase = """
        {"kind":"e2e","steps":["registrar el pago"],"expected_result":"el saldo baja"}
        """;

    [Fact]
    public async Task A_compatible_relation_answers_201_with_the_relation()
    {
        var (client, token, projectId) = await SignedInAsync();
        var story = await ArtifactAsync(client, token, projectId, "user_story", "Registrar pago", Story);
        var test = await ArtifactAsync(client, token, projectId, "test_case", "El saldo baja", TestCase);

        using var response = await PostRelationAsync(client, token, test.Id, story.Id, "validates");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var relation = await Read<RelationResponse>(response);
        Assert.Equal(test.Id, relation.SourceId);
        Assert.Equal(story.Id, relation.TargetId);
        Assert.Equal("validates", relation.Type);
        Assert.Equal("human", relation.CreatedBy.Type);
    }

    [Fact]
    public async Task An_incompatible_relation_answers_422_with_the_rule_and_what_was_allowed()
    {
        var (client, token, projectId) = await SignedInAsync();
        var story = await ArtifactAsync(client, token, projectId, "user_story", "Registrar pago", Story);
        var test = await ArtifactAsync(client, token, projectId, "test_case", "El saldo baja", TestCase);

        // Backwards: the test validates the story, not the other way round.
        using var response = await PostRelationAsync(client, token, story.Id, test.Id, "validates");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await Read<ValidationProblemDetails>(response);
        Assert.Equal("Relación no válida", problem.Title);
        Assert.Contains("user_story", string.Join(' ', problem.Errors["type"]), StringComparison.Ordinal);
        Assert.True(problem.Extensions.ContainsKey("allowedTargets"));
    }

    [Fact]
    public async Task A_relation_type_outside_the_catalog_answers_422_naming_it()
    {
        var (client, token, projectId) = await SignedInAsync();
        var story = await ArtifactAsync(client, token, projectId, "user_story", "Registrar pago", Story);
        var test = await ArtifactAsync(client, token, projectId, "test_case", "El saldo baja", TestCase);

        using var response = await PostRelationAsync(client, token, test.Id, story.Id, "inspira");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await Read<ValidationProblemDetails>(response);
        Assert.Contains("inspira", string.Join(' ', problem.Errors["type"]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_same_relation_twice_answers_409()
    {
        var (client, token, projectId) = await SignedInAsync();
        var module = await ArtifactAsync(client, token, projectId, "module", "Cobranzas", Module);
        var story = await ArtifactAsync(client, token, projectId, "user_story", "Registrar pago", Story);
        using var first = await PostRelationAsync(client, token, story.Id, module.Id, "belongs_to");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await PostRelationAsync(client, token, story.Id, module.Id, "belongs_to");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await Read<ProblemDetails>(second);
        Assert.Equal("La relación ya existe", problem.Title);
    }

    [Fact]
    public async Task An_artifact_that_does_not_exist_answers_404()
    {
        var (client, token, projectId) = await SignedInAsync();
        var story = await ArtifactAsync(client, token, projectId, "user_story", "Registrar pago", Story);

        using var response = await PostRelationAsync(client, token, story.Id, Guid.CreateVersion7(), "belongs_to");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_neighborhood_answers_the_nodes_with_their_depth_and_the_edges_between_them()
    {
        var (client, token, projectId) = await SignedInAsync();
        var module = await ArtifactAsync(client, token, projectId, "module", "Cobranzas", Module);
        var story = await ArtifactAsync(client, token, projectId, "user_story", "Registrar pago", Story);
        var test = await ArtifactAsync(client, token, projectId, "test_case", "El saldo baja", TestCase);
        await Created(PostRelationAsync(client, token, story.Id, module.Id, "belongs_to"));
        await Created(PostRelationAsync(client, token, test.Id, story.Id, "validates"));

        using var response = await client.GetWithTokenAsync($"/api/artifacts/{module.Id}/neighborhood?levels=2", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var neighborhood = await Read<NeighborhoodResponse>(response);
        Assert.Equal(module.Id, neighborhood.RootId);
        Assert.Equal(0, neighborhood.Nodes.Single(node => node.Id == module.Id).Depth);
        Assert.Equal(1, neighborhood.Nodes.Single(node => node.Id == story.Id).Depth);
        Assert.Equal(2, neighborhood.Nodes.Single(node => node.Id == test.Id).Depth);
        Assert.Equal(2, neighborhood.Edges.Count);
    }

    [Fact]
    public async Task Asking_for_more_than_three_levels_is_refused()
    {
        var (client, token, projectId) = await SignedInAsync();
        var module = await ArtifactAsync(client, token, projectId, "module", "Cobranzas", Module);

        using var response = await client.GetWithTokenAsync($"/api/artifacts/{module.Id}/neighborhood?levels=4", token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await Read<ValidationProblemDetails>(response);
        Assert.Contains("3", string.Join(' ', problem.Errors["levels"]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_orphans_of_a_project_are_the_ones_nobody_covers()
    {
        var (client, token, projectId) = await SignedInAsync();
        var covered = await ArtifactAsync(client, token, projectId, "user_story", "Con prueba", Story);
        var orphan = await ArtifactAsync(client, token, projectId, "user_story", "Sin prueba", Story);
        var test = await ArtifactAsync(client, token, projectId, "test_case", "Cubre la primera", TestCase);
        await Created(PostRelationAsync(client, token, test.Id, covered.Id, "validates"));

        using var response = await client.GetWithTokenAsync(
            $"/api/projects/{projectId}/orphans?type=user_story&missing=validates",
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orphans = await Read<List<OrphanResponse>>(response);
        Assert.Contains(orphans, artifact => artifact.Id == orphan.Id);
        Assert.DoesNotContain(orphans, artifact => artifact.Id == covered.Id);
    }

    [Fact]
    public async Task Deleting_a_relation_answers_204_and_it_stops_blocking_the_artifact()
    {
        var (client, token, projectId) = await SignedInAsync();
        var module = await ArtifactAsync(client, token, projectId, "module", "Cobranzas", Module);
        var story = await ArtifactAsync(client, token, projectId, "user_story", "Registrar pago", Story);
        using var created = await PostRelationAsync(client, token, story.Id, module.Id, "belongs_to");
        var relation = await Read<RelationResponse>(created);

        // While the relation exists the artifact cannot be deleted (HU-001 §4).
        using var blocked = await SendAsync(client, token, HttpMethod.Delete, $"/api/artifacts/{story.Id}", body: null);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        using var deleted = await SendAsync(client, token, HttpMethod.Delete, $"/api/artifacts/{story.Id}/relations/{relation.Id}", body: null);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var now = await SendAsync(client, token, HttpMethod.Delete, $"/api/artifacts/{story.Id}", body: null);
        Assert.Equal(HttpStatusCode.NoContent, now.StatusCode);
    }

    [Fact]
    public async Task A_relation_of_another_artifact_cannot_be_deleted_through_this_one()
    {
        var (client, token, projectId) = await SignedInAsync();
        var module = await ArtifactAsync(client, token, projectId, "module", "Cobranzas", Module);
        var story = await ArtifactAsync(client, token, projectId, "user_story", "Registrar pago", Story);
        var stranger = await ArtifactAsync(client, token, projectId, "user_story", "Ajena", Story);
        using var created = await PostRelationAsync(client, token, story.Id, module.Id, "belongs_to");
        var relation = await Read<RelationResponse>(created);

        using var response = await SendAsync(client, token, HttpMethod.Delete, $"/api/artifacts/{stranger.Id}/relations/{relation.Id}", body: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
            HttpMethod.Post,
            $"/api/projects/{projectId}/artifacts",
            $$"""{"type":{{JsonSerializer.Serialize(type)}},"title":{{JsonSerializer.Serialize(title)}},"content":{{content}}}""");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Read<ArtifactDetailResponse>(response)).Artifact;
    }

    private static Task<HttpResponseMessage> PostRelationAsync(HttpClient client, string token, Guid sourceId, Guid targetId, string type) =>
        SendAsync(
            client,
            token,
            HttpMethod.Post,
            $"/api/artifacts/{sourceId}/relations",
            $$"""{"targetId":"{{targetId}}","type":{{JsonSerializer.Serialize(type)}}}""");

    private static async Task Created(Task<HttpResponseMessage> call)
    {
        using var response = await call;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string token, HttpMethod method, string path, string? body)
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
