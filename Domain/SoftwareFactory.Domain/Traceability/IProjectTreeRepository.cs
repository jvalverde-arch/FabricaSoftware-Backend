namespace SoftwareFactory.Domain.Traceability;

/// <summary>
/// Reads of the project tree (HU-004). Two shapes, never the whole tree: the children of one node, or the branches
/// that a filter matches. The hierarchical relation types and the catalog order arrive as parameters because they
/// live in a single place in the application layer, not in this SQL.
/// </summary>
public interface IProjectTreeRepository
{
    /// <summary>First level of the tree: the project's <c>module</c> artifacts (hierarchy rule 1).</summary>
    Task<IReadOnlyList<TreeRow>> GetRootAsync(TreeChildrenQuery query, CancellationToken cancellationToken);

    /// <summary>Direct children of the node the route ends at, in the deterministic order of hierarchy rule 2.</summary>
    Task<IReadOnlyList<TreeRow>> GetChildrenAsync(TreeChildrenQuery query, CancellationToken cancellationToken);

    /// <summary>Artifacts of project level with no hierarchical parent, which is what «unclassified» holds (rule 3).</summary>
    Task<IReadOnlyList<TreeRow>> GetUnclassifiedAsync(TreeChildrenQuery query, CancellationToken cancellationToken);

    Task<int> CountUnclassifiedAsync(TreeChildrenQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Every node on the routes from the root down to each match, and nothing else: the branches collapse to what
    /// the filter found without the project ever being walked whole.
    /// </summary>
    Task<IReadOnlyList<TreeRow>> GetFilteredAsync(TreeFilterQuery query, CancellationToken cancellationToken);

    /// <summary>Matches the filter really has, counted without climbing to the ancestors, so the UI can say «500 of 1,243».</summary>
    Task<int> CountMatchesAsync(TreeFilterQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// What to expand. <paramref name="Path"/> is the route of the node, ancestors included: only the route says which
/// artifacts this branch already went through, and that is what keeps a cycle from coming back on itself (rule 5).
/// </summary>
public sealed record TreeChildrenQuery(Guid ProjectId, IReadOnlyList<Guid> Path);

/// <summary>What to filter by (HU-004 §2) and the two limits the tree refuses to cross.</summary>
public sealed record TreeFilterQuery(Guid ProjectId)
{
    public string? Type { get; init; }

    public ArtifactState? State { get; init; }

    public string? Text { get; init; }

    public int MaxDepth { get; init; }

    public int MaxMatches { get; init; }
}

/// <summary>
/// A node as the database returns it. <paramref name="Path"/> is its identity in the UI (rule 4) and is made of ids;
/// a title can be edited and must never change what a node is.
/// </summary>
public sealed record TreeRow(
    IReadOnlyList<Guid> Path,
    Guid Id,
    string? Type,
    string? Title,
    ArtifactState? State,
    int? Score,
    string? EdgeType,
    int ChildCount,
    bool Matches);
