using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SoftwareFactory.Api.Contracts.Artifacts;
using SoftwareFactory.Api.Contracts.Tree;

namespace SoftwareFactory.Api.Tests.Integration;

/// <summary>
/// The contract of HU-004 over HTTP: the two modes, the route as the identity of a node, and the bucket that keeps
/// the tree from hiding anything.
/// </summary>
[Collection(ApiCollectionDefinition.Name)]
public sealed class TreeEndpointsTests(ApiFixture fixture)
{
    private const string Module = """{"name":"Cobranzas","purpose":"Cobrar lo que se debe"}""";

    private const string Story = """
        {"as_a":"cajera","i_want":"registrar un pago","so_that":"la deuda baje","acceptance_criteria":["el saldo baja"]}
        """;

    private const string Rule = """{"statement":"No se cobra dos veces la misma deuda"}""";

    [Fact]
    public async Task The_root_answers_the_modules_of_the_project_with_their_child_count()
    {
        var (client, token, projectId) = await SignedInAsync();
        var module = await ArtifactAsync(client, token, projectId, "module", $"Cobranzas {Guid.NewGuid():N}", Module);
        var story = await ArtifactAsync(client, token, projectId, "user_story", $"Registrar pago {Guid.NewGuid():N}", Story);
        await RelateAsync(client, token, story.Id, module.Id, "belongs_to");

        using var response = await SendAsync(client, token, $"/api/projects/{projectId}/tree");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tree = await Read<TreeResponse>(response);
        Assert.False(tree.Expanded);

        var node = Assert.Single(tree.Nodes, item => item.Id == module.Id);
        Assert.Equal("artifact", node.Kind);
        Assert.Equal(module.Id.ToString(), node.Path);
        Assert.Equal(1, node.ChildCount);
        Assert.Equal(1, node.Depth);
    }

    [Fact]
    public async Task Expanding_a_node_answers_its_children_with_the_route_that_reached_them()
    {
        var (client, token, projectId) = await SignedInAsync();
        var module = await ArtifactAsync(client, token, projectId, "module", $"Cobranzas {Guid.NewGuid():N}", Module);
        var story = await ArtifactAsync(client, token, projectId, "user_story", $"Registrar pago {Guid.NewGuid():N}", Story);
        await RelateAsync(client, token, story.Id, module.Id, "belongs_to");

        using var response = await SendAsync(client, token, $"/api/projects/{projectId}/tree?node={module.Id}&level=1");

        var tree = await Read<TreeResponse>(response);
        var child = Assert.Single(tree.Nodes);
        Assert.Equal(story.Id, child.Id);
        Assert.Equal($"{module.Id}/{story.Id}", child.Path);
        Assert.Equal("belongs_to", child.EdgeType);
        Assert.Equal(2, child.Depth);
    }

    [Fact]
    public async Task The_filter_answers_the_branches_of_the_matches_already_expanded()
    {
        var (client, token, projectId) = await SignedInAsync();
        var mark = Guid.NewGuid().ToString("N");
        var module = await ArtifactAsync(client, token, projectId, "module", $"Cobranzas {mark}", Module);
        var story = await ArtifactAsync(client, token, projectId, "user_story", $"Registrar pago {mark}", Story);
        await RelateAsync(client, token, story.Id, module.Id, "belongs_to");

        using var response = await SendAsync(client, token, $"/api/projects/{projectId}/tree?text=Registrar pago {mark}");

        var tree = await Read<TreeResponse>(response);
        Assert.True(tree.Expanded);
        Assert.Equal(1, tree.MatchCount);
        Assert.False(tree.Truncated);
        Assert.Equal([module.Id.ToString(), $"{module.Id}/{story.Id}"], tree.Nodes.Select(node => node.Path));
        Assert.Equal([false, true], tree.Nodes.Select(node => node.Matches));
    }

    [Fact]
    public async Task An_artifact_with_no_hierarchical_parent_answers_inside_the_unclassified_bucket()
    {
        var (client, token, projectId) = await SignedInAsync();
        var loose = await ArtifactAsync(client, token, projectId, "business_rule", $"Regla suelta {Guid.NewGuid():N}", Rule);

        using var root = await SendAsync(client, token, $"/api/projects/{projectId}/tree");
        var bucket = Assert.Single((await Read<TreeResponse>(root)).Nodes, node => node.Kind == "unclassified");
        Assert.Null(bucket.Title);

        using var inside = await SendAsync(client, token, $"/api/projects/{projectId}/tree?node={bucket.Path}&level=1");

        var nodes = (await Read<TreeResponse>(inside)).Nodes;
        Assert.Contains(nodes, node => node.Id == loose.Id && node.Path == $"{bucket.Path}/{loose.Id}");
    }

    [Fact]
    public async Task A_route_that_is_not_made_of_identifiers_answers_422()
    {
        var (client, token, projectId) = await SignedInAsync();

        using var response = await SendAsync(client, token, $"/api/projects/{projectId}/tree?node=Cobranzas");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await Read<ValidationProblemDetails>(response);
        Assert.True(problem.Errors.ContainsKey("node"));
    }

    [Fact]
    public async Task A_level_that_disagrees_with_the_route_answers_422()
    {
        var (client, token, projectId) = await SignedInAsync();
        var module = await ArtifactAsync(client, token, projectId, "module", $"Cobranzas {Guid.NewGuid():N}", Module);

        using var response = await SendAsync(client, token, $"/api/projects/{projectId}/tree?node={module.Id}&level=3");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await Read<ValidationProblemDetails>(response);
        Assert.True(problem.Errors.ContainsKey("level"));
    }

    /// <summary>
    /// The published document is not a nicety here: the frontend of this same HU generates its typed client from it
    /// (estandar-ui §7.5), so an endpoint missing from the document is an endpoint the web cannot call at all.
    /// </summary>
    [Fact]
    public async Task The_endpoint_travels_in_the_published_openapi_document()
    {
        var client = fixture.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/projects/{projectId}/tree", out var tree));
        var parameters = tree.GetProperty("get").GetProperty("parameters").EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString())
            .ToList();

        Assert.Equal<string?[]>(["projectId", "node", "level", "type", "state", "text"], [.. parameters]);
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

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string token,
        string path,
        HttpMethod? method = null,
        string? body = null)
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
