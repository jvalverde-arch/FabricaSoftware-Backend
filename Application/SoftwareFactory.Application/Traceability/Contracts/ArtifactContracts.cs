namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>Who writes: «human» or «agent» (doc 01, principles 6-7).</summary>
public sealed record ArtifactAuthor(string Type, Guid Id);

/// <summary>Creates an artifact with its first version of content (HU-001 §1).</summary>
public sealed record CreateArtifactCommand(Guid ProjectId, string Type, string Title, string Content)
{
    /// <summary>«project» (default) or «global».</summary>
    public string Level { get; init; } = ArtifactNames.ProjectLevel;
}

/// <summary>Edits an artifact; every edit produces a new version (HU-001 §2).</summary>
public sealed record UpdateArtifactCommand(Guid ArtifactId, string? Title, string? Content)
{
    /// <summary>Optional state move by wire name; the allowed transitions live in the entity.</summary>
    public string? State { get; init; }
}

/// <summary>Artifact as the rest of the platform sees it.</summary>
public sealed record ArtifactDto(
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

/// <summary>Artifact with the content of its current version, already upgraded for reading if needed.</summary>
public sealed record ArtifactDetailDto(ArtifactDto Artifact, string Content, int SchemaVersion, ArtifactAuthor LastAuthor);

public sealed record ArtifactVersionDto(int Number, int SchemaVersion, ArtifactAuthor Author, DateTimeOffset CreatedAt);

/// <summary>Difference between two versions, field by field (HU-001 contracts).</summary>
public sealed record ArtifactDiffDto(int FromVersion, int ToVersion, IReadOnlyList<ArtifactFieldChange> Changes);

public sealed record ArtifactFieldChange(string Path, string? From, string? To, ArtifactChangeKind Kind);

public enum ArtifactChangeKind
{
    Added,
    Removed,
    Modified,
}

public sealed record ArtifactPage(IReadOnlyList<ArtifactDto> Items, int Total, int Skip, int Take);
