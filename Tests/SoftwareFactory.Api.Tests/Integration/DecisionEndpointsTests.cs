using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SoftwareFactory.Api.Contracts.Artifacts;
using SoftwareFactory.Api.Contracts.Decisions;
using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Api.Tests.Integration;

/// <summary>
/// Contracts of HU-003 over HTTP: the server derives what kind of entry it is, the snapshot says who may close it,
/// and the two conditions of §4 — a competent role, and not being the author — are both enforced.
/// </summary>
[Collection(ApiCollectionDefinition.Name)]
public sealed class DecisionEndpointsTests(ApiFixture fixture)
{
    private const string Story = """
        {"as_a":"cajera","i_want":"registrar un pago","so_that":"la deuda baje","acceptance_criteria":["el saldo baja"]}
        """;

    private const string Component = """{"responsibility":"Cobrar con la pasarela","technology":"dotnet"}""";

    private const string Password = "Decision-Password-For-Tests-2026";

    [Fact]
    public async Task Touching_what_answers_to_another_role_answers_201_with_a_pending_note()
    {
        var (client, projectId) = await ProjectAsync();
        var functional = await SignInAsync(client, Role.Functional);
        var component = await ArtifactAsync(client, functional, projectId, "architecture_component", "Pasarela", Component);

        using var response = await RecordAsync(client, functional, projectId, [component.Id], "Hay que cambiar la pasarela.");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var note = await Read<DecisionResponse>(response);
        Assert.Equal("out_of_role_note", note.Type);
        Assert.Equal("pending", note.State);
        Assert.Equal(["architect"], note.CompetentRoles);
        Assert.Equal(["functional"], note.AuthorRoles);
        Assert.Equal([component.Id], note.ArtifactIds);
    }

    [Fact]
    public async Task An_author_competent_for_what_they_touched_answers_201_with_a_closed_decision()
    {
        var (client, projectId) = await ProjectAsync();
        var functional = await SignInAsync(client, Role.Functional);
        var story = await ArtifactAsync(client, functional, projectId, "user_story", "Registrar pago", Story);

        using var response = await RecordAsync(client, functional, projectId, [story.Id], "Cambia el criterio de aceptación.");

        var decision = await Read<DecisionResponse>(response);
        Assert.Equal("decision", decision.Type);
        Assert.Equal("recorded", decision.State);
        Assert.Empty(decision.CompetentRoles);
    }

    [Fact]
    public async Task A_competent_role_ratifies_the_note_and_the_child_points_back_at_it()
    {
        var (client, projectId) = await ProjectAsync();
        var functional = await SignInAsync(client, Role.Functional);
        var component = await ArtifactAsync(client, functional, projectId, "architecture_component", "Pasarela", Component);
        var note = await Read<DecisionResponse>(await RecordAsync(client, functional, projectId, [component.Id], "Cambiemos la pasarela."));

        var architect = await SignInAsync(client, Role.Architect);
        using var response = await CloseAsync(client, architect, note.Id, "ratify", "La arquitectura lo sostiene.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ratification = await Read<DecisionResponse>(response);
        Assert.Equal("ratification", ratification.Type);
        Assert.Equal(note.Id, ratification.ParentDecisionId);
        Assert.Equal(["architect"], ratification.AuthorRoles);
    }

    [Fact]
    public async Task A_role_outside_the_snapshot_answers_403_naming_who_can()
    {
        var (client, projectId) = await ProjectAsync();
        var functional = await SignInAsync(client, Role.Functional);
        var component = await ArtifactAsync(client, functional, projectId, "architecture_component", "Pasarela", Component);
        var note = await Read<DecisionResponse>(await RecordAsync(client, functional, projectId, [component.Id], "Cambiemos la pasarela."));

        var qa = await SignInAsync(client, Role.Qa);
        using var response = await CloseAsync(client, qa, note.Id, "ratify", "Me parece bien.");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await Read<ProblemDetails>(response);
        Assert.Contains("architect", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_author_promoted_to_the_competent_role_still_answers_403_self_ratification()
    {
        var (client, projectId) = await ProjectAsync();
        var email = $"decision-{Guid.NewGuid():N}@local";
        var user = await fixture.CreateUserAsync(email, Password, Role.Functional);
        var functional = (await client.LoginAsync(email, Password)).AccessToken;

        var component = await ArtifactAsync(client, functional, projectId, "architecture_component", "Pasarela", Component);
        var note = await Read<DecisionResponse>(await RecordAsync(client, functional, projectId, [component.Id], "Cambiemos la pasarela."));
        Assert.Equal(["architect"], note.CompetentRoles);

        // The same person, promoted to architect and signed in again: they now clear the role check of §4, so only
        // the identity guard stands between them and ratifying their own note.
        await fixture.GrantRoleAsync(user.Id, Role.Architect);
        var promoted = (await client.LoginAsync(email, Password)).AccessToken;

        using var response = await CloseAsync(client, promoted, note.Id, "ratify", "Me la ratifico yo.");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await Read<ProblemDetails>(response);
        Assert.Equal("self_ratification", problem.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Pending_notes_of_a_role_are_listed_by_the_query()
    {
        var (client, projectId) = await ProjectAsync();
        var functional = await SignInAsync(client, Role.Functional);
        var component = await ArtifactAsync(client, functional, projectId, "architecture_component", "Pasarela", Component);
        var note = await Read<DecisionResponse>(await RecordAsync(client, functional, projectId, [component.Id], "Cambiemos la pasarela."));

        using var response = await SendAsync(client, functional, HttpMethod.Get, $"/api/projects/{projectId}/decisions?pending_role=architect", body: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await Read<DecisionPageResponse>(response);
        Assert.Contains(page.Items, item => item.Id == note.Id);
        Assert.All(page.Items, item => Assert.Equal("pending", item.State));
    }

    [Fact]
    public async Task Decisions_of_one_artifact_include_the_note_and_its_ratification()
    {
        var (client, projectId) = await ProjectAsync();
        var functional = await SignInAsync(client, Role.Functional);
        var component = await ArtifactAsync(client, functional, projectId, "architecture_component", "Pasarela", Component);
        var note = await Read<DecisionResponse>(await RecordAsync(client, functional, projectId, [component.Id], "Cambiemos la pasarela."));

        var architect = await SignInAsync(client, Role.Architect);
        using (var ratified = await CloseAsync(client, architect, note.Id, "revert", "Mejor no."))
        {
            Assert.Equal(HttpStatusCode.OK, ratified.StatusCode);
        }

        using var response = await SendAsync(client, architect, HttpMethod.Get, $"/api/projects/{projectId}/decisions?artifact_id={component.Id}", body: null);

        var page = await Read<DecisionPageResponse>(response);
        Assert.Equal(2, page.Items.Count(item => item.ArtifactIds.Contains(component.Id)));
        Assert.Contains(page.Items, item => string.Equals(item.Type, "reversion", StringComparison.Ordinal));
        Assert.Contains(page.Items, item => item.Id == note.Id && string.Equals(item.State, "reverted", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_competence_map_is_readable_for_the_artifact_card()
    {
        var (client, _) = await ProjectAsync();
        var reader = await SignInAsync(client, Role.Reader);

        using var response = await SendAsync(client, reader, HttpMethod.Get, "/api/config/artifact-type-roles", body: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var map = await Read<IReadOnlyList<ArtifactTypeRoleResponse>>(response);
        var requirement = Assert.Single(map, entry => string.Equals(entry.ArtifactType, "non_functional_requirement", StringComparison.Ordinal));
        Assert.Equal(["functional", "architect"], requirement.Roles);
    }

    [Fact]
    public async Task A_decision_without_artifacts_answers_422()
    {
        var (client, projectId) = await ProjectAsync();
        var functional = await SignInAsync(client, Role.Functional);

        using var response = await RecordAsync(client, functional, projectId, [], "Porque sí.");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private async Task<(HttpClient Client, Guid ProjectId)> ProjectAsync()
    {
        var client = fixture.CreateClient();
        return (client, await fixture.LocalProjectIdAsync());
    }

    private async Task<string> SignInAsync(HttpClient client, params Role[] roles)
    {
        var email = $"decision-{Guid.NewGuid():N}@local";
        await fixture.CreateUserAsync(email, Password, roles);
        var session = await client.LoginAsync(email, Password);
        return session.AccessToken;
    }

    private static Task<HttpResponseMessage> RecordAsync(HttpClient client, string token, Guid projectId, Guid[] artifactIds, string justification) =>
        SendAsync(
            client,
            token,
            HttpMethod.Post,
            $"/api/projects/{projectId}/decisions",
            $$"""{"artifactIds":{{JsonSerializer.Serialize(artifactIds)}},"justification":{{JsonSerializer.Serialize(justification)}}}""");

    private static Task<HttpResponseMessage> CloseAsync(HttpClient client, string token, Guid decisionId, string action, string justification) =>
        SendAsync(
            client,
            token,
            HttpMethod.Post,
            $"/api/decisions/{decisionId}/{action}",
            $$"""{"justification":{{JsonSerializer.Serialize(justification)}}}""");

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
