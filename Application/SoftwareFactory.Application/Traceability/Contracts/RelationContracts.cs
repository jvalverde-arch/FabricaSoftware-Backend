namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>Creates a typed, directed relation between two artifacts (HU-002 §1).</summary>
public sealed record CreateRelationCommand(Guid SourceId, Guid TargetId, string Type)
{
    /// <summary>Optional JSON metadata of the relation (HU-002 §5).</summary>
    public string? Metadata { get; init; }
}

public sealed record RelationDto(
    Guid Id,
    Guid SourceId,
    Guid TargetId,
    string Type,
    string? Metadata,
    ArtifactAuthor CreatedBy,
    DateTimeOffset CreatedAt);

/// <summary>An artifact reached by the neighborhood query, with how far it is from the one that was asked about.</summary>
public sealed record NeighborhoodNode(
    Guid Id,
    Guid ProjectId,
    string Type,
    string Title,
    string State,
    string Level,
    int? Score,
    int Depth);

/// <summary>An edge between two nodes of the neighborhood, with its direction seen from the root.</summary>
public sealed record NeighborhoodEdge(Guid Id, Guid SourceId, Guid TargetId, string Type);

/// <summary>Everything within N levels of an artifact, in a single call (HU-002 §3).</summary>
public sealed record NeighborhoodDto(Guid RootId, int Levels, IReadOnlyList<NeighborhoodNode> Nodes, IReadOnlyList<NeighborhoodEdge> Edges);

/// <summary>An artifact of the asked type that has no relation of the asked type, in either direction (HU-002 §4).</summary>
public sealed record OrphanDto(Guid Id, string Type, string Title, string State, int? Score);
