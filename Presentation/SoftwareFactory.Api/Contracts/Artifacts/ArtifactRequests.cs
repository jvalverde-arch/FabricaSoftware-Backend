using System.Text.Json;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Api.Contracts.Artifacts;

/// <summary>Creates an artifact with its first version of content (HU-001 §1).</summary>
public sealed record CreateArtifactRequest(string Type, string Title, JsonElement Content)
{
    /// <summary>«project» (default) or «global»; a global artifact is inherited by every project of the tenant.</summary>
    public string? Level { get; init; }
}

/// <summary>Edits an artifact. Every field is optional: send only what changes (HU-001 §2).</summary>
public sealed record UpdateArtifactRequest(string? Title, JsonElement? Content, string? State);

public sealed record SetArtifactScoreRequest(int? Score);

/// <summary>Artifact as the Api exposes it.</summary>
public sealed record ArtifactResponse(
    Guid Id,
    Guid ProjectId,
    string Type,
    string Title,
    string State,
    string Level,
    int? Score,
    int CurrentVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Artifact with the content of its current version and the schema that content conforms to.</summary>
public sealed record ArtifactDetailResponse(
    ArtifactResponse Artifact,
    JsonElement Content,
    int SchemaVersion,
    ArtifactAuthorResponse LastAuthor);

public sealed record ArtifactAuthorResponse(string Type, Guid Id);

public sealed record ArtifactVersionResponse(int Number, int SchemaVersion, ArtifactAuthorResponse Author, DateTimeOffset CreatedAt);

public sealed record ArtifactDiffResponse(int FromVersion, int ToVersion, IReadOnlyList<ArtifactChangeResponse> Changes);

public sealed record ArtifactChangeResponse(string Path, string? From, string? To, string Kind);

public sealed record ArtifactPageResponse(IReadOnlyList<ArtifactResponse> Items, int Total, int Skip, int Take);

/// <summary>A relation that still points at an artifact and keeps it from being deleted (HU-001 §4).</summary>
public sealed record BlockingRelationResponse(Guid RelationId, string Type, Guid ArtifactId, string Title, string Direction)
{
    public static BlockingRelationResponse From(BlockingRelation relation)
    {
        ArgumentNullException.ThrowIfNull(relation);

        return new BlockingRelationResponse(
            relation.RelationId,
            relation.Type,
            relation.OtherArtifactId,
            relation.OtherArtifactTitle,
            relation.Direction);
    }
}
