using System.Text.Json;
using SoftwareFactory.Api.Contracts.Artifacts;
using SoftwareFactory.Api.Contracts.Decisions;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Api.Contracts.Cards;

/// <summary>One relation of the card, with the marks the panel needs to warn before changing context (HU-005 §2).</summary>
public sealed record CardRelationResponse(
    Guid RelationId,
    string Type,
    Guid ArtifactId,
    string ArtifactType,
    string Title,
    string State,
    int? Score,
    Guid ProjectId,
    string ProjectName,
    Guid? ModuleId,
    string? ModuleTitle,
    bool CrossesModule,
    bool CrossesProject);

public sealed record CardRelationGroupResponse(string Type, IReadOnlyList<CardRelationResponse> Items);

/// <summary>The JSON Schema of the version being shown, so the web can draw the content without a second call.</summary>
public sealed record ArtifactSchemaResponse(string Type, int Version, JsonElement Json);

public sealed record ArtifactVersionSummaryResponse(int Number, int SchemaVersion, ArtifactAuthorResponse Author, DateTimeOffset CreatedAt);

public sealed record ArtifactAuthorResponse(string Type, Guid Id);

/// <summary>
/// Everything the artifact card draws, in one answer (HU-005). <c>schema</c> is the one of <c>version</c> and not
/// the current one of the type: that is what keeps an old version from being labelled with today's field names.
/// </summary>
public sealed record ArtifactCardResponse(
    ArtifactResponse Artifact,
    int Version,
    JsonElement Content,
    int SchemaVersion,
    ArtifactSchemaResponse Schema,
    ArtifactAuthorResponse Author,
    IReadOnlyList<CardRelationGroupResponse> Upstream,
    IReadOnlyList<CardRelationGroupResponse> Downstream,
    IReadOnlyList<ArtifactVersionSummaryResponse> Versions,
    IReadOnlyList<DecisionResponse> Decisions);

/// <summary>Turns the card contract of the module into the wire shape.</summary>
public static class CardMapping
{
    public static ArtifactCardResponse ToResponse(this ArtifactCardDto card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return new ArtifactCardResponse(
            card.Artifact.ToResponse(),
            card.Version,
            Parse(card.Content),
            card.SchemaVersion,
            new ArtifactSchemaResponse(card.Schema.Type, card.Schema.Version, Parse(card.Schema.Json)),
            new ArtifactAuthorResponse(card.Author.Type, card.Author.Id),
            [.. card.Upstream.Select(ToResponse)],
            [.. card.Downstream.Select(ToResponse)],
            [
                .. card.Versions.Select(version => new ArtifactVersionSummaryResponse(
                    version.Number,
                    version.SchemaVersion,
                    new ArtifactAuthorResponse(version.Author.Type, version.Author.Id),
                    version.CreatedAt)),
            ],
            [.. card.Decisions.Select(decision => decision.ToResponse())]);
    }

    private static CardRelationGroupResponse ToResponse(CardRelationGroup group) =>
        new(
            group.Type,
            [
                .. group.Items.Select(item => new CardRelationResponse(
                    item.RelationId,
                    item.Type,
                    item.ArtifactId,
                    item.ArtifactType,
                    item.Title,
                    item.State,
                    item.Score,
                    item.ProjectId,
                    item.ProjectName,
                    item.ModuleId,
                    item.ModuleTitle,
                    item.CrossesModule,
                    item.CrossesProject)),
            ]);

    /// <summary>Content and schema travel as JSON, not as a string carrying JSON: the client parses nothing twice.</summary>
    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
