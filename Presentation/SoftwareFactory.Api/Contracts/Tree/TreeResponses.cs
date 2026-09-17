using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Api.Contracts.Tree;

/// <summary>
/// A node of the project tree (HU-004). <paramref name="Path"/> is its identity: the route from the root, made of
/// ids and joined by «/». The UI keys on it and not on <paramref name="Id"/>, because an artifact with two parents
/// appears under both and keying by id would make expanding one copy expand the other.
/// </summary>
public sealed record TreeNodeResponse(
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
/// The answer of the tree. In lazy mode (<c>expanded: false</c>) the nodes are the children of the node asked about
/// and the client keeps expanding; in filtered mode (<c>expanded: true</c>) they are the whole collapsed branch set
/// and there is nothing left to fetch. <paramref name="MatchCount"/> is what the filter really found, so a truncated
/// answer can say «500 of 1,243» instead of only warning.
/// </summary>
public sealed record TreeResponse(
    string Node,
    int Level,
    bool Expanded,
    IReadOnlyList<TreeNodeResponse> Nodes,
    int MatchCount,
    bool Truncated);

/// <summary>Turns the tree contracts of the module into the wire shape.</summary>
public static class TreeMapping
{
    public static TreeResponse ToResponse(this ProjectTreeDto tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        return new TreeResponse(
            tree.Node,
            tree.Level,
            tree.Expanded,
            [.. tree.Nodes.Select(node => new TreeNodeResponse(
                node.Path,
                node.Id,
                node.Kind,
                node.Type,
                node.Title,
                node.State,
                node.Score,
                node.EdgeType,
                node.Depth,
                node.ChildCount,
                node.Matches))],
            tree.MatchCount,
            tree.Truncated);
    }
}
