using System.Text.Json;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Api.Contracts.Relations;

/// <summary>Creates a typed relation from the artifact in the route to <paramref name="TargetId"/> (HU-002 §1).</summary>
public sealed record CreateRelationRequest(Guid TargetId, string Type)
{
    /// <summary>Optional JSON metadata of the relation (HU-002 §5).</summary>
    public JsonElement? Metadata { get; init; }
}

public sealed record RelationResponse(
    Guid Id,
    Guid SourceId,
    Guid TargetId,
    string Type,
    JsonElement? Metadata,
    RelationAuthorResponse CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record RelationAuthorResponse(string Type, Guid Id);

public sealed record NeighborhoodNodeResponse(
    Guid Id,
    Guid ProjectId,
    string Type,
    string Title,
    string State,
    string Level,
    int? Score,
    int Depth);

public sealed record NeighborhoodEdgeResponse(Guid Id, Guid SourceId, Guid TargetId, string Type);

/// <summary>The artifacts around one artifact and the relations between them, in a single answer (HU-002 §3).</summary>
public sealed record NeighborhoodResponse(
    Guid RootId,
    int Levels,
    IReadOnlyList<NeighborhoodNodeResponse> Nodes,
    IReadOnlyList<NeighborhoodEdgeResponse> Edges);

public sealed record OrphanResponse(Guid Id, string Type, string Title, string State, int? Score);

/// <summary>Turns the relation contracts of the module into the wire shape.</summary>
public static class RelationMapping
{
    public static RelationResponse ToResponse(this RelationDto relation)
    {
        ArgumentNullException.ThrowIfNull(relation);

        return new RelationResponse(
            relation.Id,
            relation.SourceId,
            relation.TargetId,
            relation.Type,
            Parse(relation.Metadata),
            new RelationAuthorResponse(relation.CreatedBy.Type, relation.CreatedBy.Id),
            relation.CreatedAt);
    }

    public static NeighborhoodResponse ToResponse(this NeighborhoodDto neighborhood)
    {
        ArgumentNullException.ThrowIfNull(neighborhood);

        return new NeighborhoodResponse(
            neighborhood.RootId,
            neighborhood.Levels,
            [.. neighborhood.Nodes.Select(node => new NeighborhoodNodeResponse(
                node.Id,
                node.ProjectId,
                node.Type,
                node.Title,
                node.State,
                node.Level,
                node.Score,
                node.Depth))],
            [.. neighborhood.Edges.Select(edge => new NeighborhoodEdgeResponse(edge.Id, edge.SourceId, edge.TargetId, edge.Type))]);
    }

    public static OrphanResponse ToResponse(this OrphanDto orphan)
    {
        ArgumentNullException.ThrowIfNull(orphan);

        return new OrphanResponse(orphan.Id, orphan.Type, orphan.Title, orphan.State, orphan.Score);
    }

    private static JsonElement? Parse(string? json)
    {
        if (json is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
