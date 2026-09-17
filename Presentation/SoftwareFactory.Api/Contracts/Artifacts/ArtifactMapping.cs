using System.Text.Json;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Api.Contracts.Artifacts;

/// <summary>
/// Turns the module contracts into the wire shape. States, levels and author types already travel as the snake_case
/// names the module publishes, so this only reshapes records — the Api never touches the domain enums.
/// </summary>
public static class ArtifactMapping
{
    public static ArtifactResponse ToResponse(this ArtifactDto artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        return new ArtifactResponse(
            artifact.Id,
            artifact.ProjectId,
            artifact.Type,
            artifact.Title,
            artifact.State,
            artifact.Level,
            artifact.Score,
            artifact.CurrentVersion,
            artifact.CreatedAt,
            artifact.UpdatedAt);
    }

    public static ArtifactDetailResponse ToResponse(this ArtifactDetailDto detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        return new ArtifactDetailResponse(
            detail.Artifact.ToResponse(),
            Parse(detail.Content),
            detail.SchemaVersion,
            new ArtifactAuthorResponse(detail.LastAuthor.Type, detail.LastAuthor.Id));
    }

    public static ArtifactPageResponse ToResponse(this ArtifactPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return new ArtifactPageResponse([.. page.Items.Select(ToResponse)], page.Total, page.Skip, page.Take);
    }

    public static ArtifactVersionResponse ToResponse(this ArtifactVersionDto version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return new ArtifactVersionResponse(
            version.Number,
            version.SchemaVersion,
            new ArtifactAuthorResponse(version.Author.Type, version.Author.Id),
            version.CreatedAt);
    }

    public static ArtifactDiffResponse ToResponse(this ArtifactDiffDto diff)
    {
        ArgumentNullException.ThrowIfNull(diff);

        return new ArtifactDiffResponse(
            diff.FromVersion,
            diff.ToVersion,
            [.. diff.Changes.Select(change => new ArtifactChangeResponse(change.Path, change.From, change.To, NameOf(change.Kind)))]);
    }

    private static string NameOf(ArtifactChangeKind kind) => kind switch
    {
        ArtifactChangeKind.Added => "added",
        ArtifactChangeKind.Removed => "removed",
        _ => "modified",
    };

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
