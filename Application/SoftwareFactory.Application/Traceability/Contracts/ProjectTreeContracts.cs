namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>Filters of the tree (HU-004 §2) as callers express them; any of them switches the query to filtered mode.</summary>
public sealed record ProjectTreeFilter
{
    public string? Type { get; init; }

    /// <summary>draft | in_review | approved | frozen.</summary>
    public string? State { get; init; }

    /// <summary>Fragment of the title, case-insensitive.</summary>
    public string? Text { get; init; }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Type) && string.IsNullOrWhiteSpace(State) && string.IsNullOrWhiteSpace(Text);
}

/// <summary>What the caller asks the tree for: the children of a node, or the branches that match a filter.</summary>
public sealed record ProjectTreeQuery(Guid ProjectId)
{
    /// <summary>
    /// Route of the node to expand, as the server handed it out: ids joined by «/», never titles (hierarchy rule 4).
    /// Empty means the root. It is the route and not the id because only the route says which ancestors this branch
    /// already went through, and that is what keeps a cycle from coming back on itself.
    /// </summary>
    public string? Node { get; init; }

    /// <summary>Depth of the node being expanded; it must agree with <see cref="Node"/>.</summary>
    public int Level { get; init; }

    public ProjectTreeFilter Filter { get; init; } = new();
}

/// <summary>A node of the tree. Its identity is the route, which is what the UI keys on (hierarchy rule 4).</summary>
public sealed record ProjectTreeNodeDto(
    string Path,
    Guid Id,
    string Kind,
    string? Type,
    string? Title,
    string? State,
    int? Score,
    string? EdgeType,
    int Depth,
    int ChildCount,
    bool Matches);

/// <summary>
/// The answer of the tree endpoint. <see cref="Expanded"/> tells the client which mode it got: in lazy mode the
/// nodes are the children of the node asked about and the client keeps expanding; in filtered mode they are the
/// whole collapsed branch set and there is nothing left to fetch.
/// </summary>
public sealed record ProjectTreeDto(
    string Node,
    int Level,
    bool Expanded,
    IReadOnlyList<ProjectTreeNodeDto> Nodes,
    int MatchCount,
    bool Truncated);

/// <summary>Kinds of node the tree returns: a real artifact, or the synthetic bucket of hierarchy rule 3.</summary>
public static class TreeNodeKinds
{
    public const string Artifact = "artifact";

    /// <summary>Everything of project level with no hierarchical parent. It has no title: the frontend names it.</summary>
    public const string Unclassified = "unclassified";

    /// <summary>
    /// Fixed id of the synthetic bucket. It is not an artifact, but a route is made of ids and nothing else
    /// (hierarchy rule 4), so the bucket needs one of its own to be able to appear in a route.
    /// </summary>
    public static Guid UnclassifiedId { get; } = Guid.Parse("01920000-0000-7000-8000-00000000f001");
}
