using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Application.Traceability.Contracts;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Repositories;

/// <summary>
/// The project tree (HU-004) as raw SQL, because both shapes it serves are recursive or array work that LINQ cannot
/// express. Row-level security does the tenant filtering. Neither the hierarchical relation types nor the catalog
/// order are written here: they arrive as arrays and the query reads their precedence with <c>WITH ORDINALITY</c>,
/// so the order of the C# lists is the order of the SQL and the two cannot drift apart.
/// </summary>
public sealed class ProjectTreeRepository(SoftwareFactoryDbContext context) : IProjectTreeRepository
{
    /// <summary>
    /// Head of every query: the two ordered lists, unnested so the SQL can read their order without naming a single
    /// type. What an artifact of this tree is — this project, project level, alive — is spelled out wherever it is
    /// needed instead of being materialised once: a walk reaches its neighbours through the relation indexes, and a
    /// set materialised up front would have to be scanned whole on every step of the recursion.
    /// </summary>
    private const string PreludeBody = """
        hierarchy AS (
            SELECT type, ordinality AS precedence FROM unnest(@types) WITH ORDINALITY AS t(type, ordinality)
        ),
        catalog AS (
            SELECT type, ordinality AS position FROM unnest(@catalog) WITH ORDINALITY AS c(type, ordinality)
        )
        """;

    private const string Prelude = $"WITH {PreludeBody}";

    /// <summary>The same head, for the one query that walks upward: a self-referencing CTE needs the whole WITH to
    /// be declared recursive, not just its own branch.</summary>
    private const string RecursivePrelude = $"WITH RECURSIVE {PreludeBody}";

    /// <summary>
    /// An artifact belongs to the tree when it is of this project, of project level and not deleted. The last one is
    /// what keeps the tenant's global artifacts out of it (hierarchy rule 6).
    /// </summary>
    /// <remarks>
    /// One per alias and spelled out: a single fragment without the alias on every column compiles, runs, and quietly
    /// resolves <c>level</c> against whichever table happens to be unambiguous at that point in the query.
    /// </remarks>
    private const string VisibleNode = "v.project_id = @project AND v.level = 'project' AND v.deleted_at IS NULL";

    private const string VisibleChild = "child.project_id = @project AND child.level = 'project' AND child.deleted_at IS NULL";

    private const string VisibleParent = "parent.project_id = @project AND parent.level = 'project' AND parent.deleted_at IS NULL";

    /// <summary>
    /// How many children a node would show if it were expanded. It takes the node's own route so the count never
    /// includes an ancestor: promising a child that the walk will refuse to follow (rule 5) shows a chevron that
    /// expands into nothing. <c>DISTINCT</c> because a pair joined by two hierarchical types is still one child.
    /// </summary>
    private const string ChildCountSql = $"""
        (SELECT count(DISTINCT r.source_id)::int
         FROM relation r
         JOIN hierarchy h ON h.type = r.type
         JOIN artifact child ON child.id = r.source_id AND {VisibleChild}
         WHERE r.target_id = v.id AND NOT (r.source_id = ANY(@path || v.id)))
        """;

    private const string RootSql = $"""
        {Prelude}
        SELECT v.id AS "Id", v.type AS "Type", v.title AS "Title", v.state AS "State", v.score AS "Score",
               NULL::text AS "EdgeType", {ChildCountSql} AS "ChildCount"
        FROM artifact v
        LEFT JOIN catalog c ON c.type = v.type
        WHERE {VisibleNode} AND v.type = @moduleType
        ORDER BY c.position, v.title, v.id
        """;

    /// <summary>
    /// Children of one node. The edges are read by the index over the far end of the relation, and the pair is
    /// folded to its winning type: when two artifacts are joined by more than one hierarchical relation, the lowest
    /// ordinal wins, which is the precedence of rule 2.
    /// </summary>
    private const string ChildrenSql = $"""
        {Prelude},
        child AS (
            SELECT r.source_id AS id, MIN(h.precedence) AS precedence
            FROM relation r
            JOIN hierarchy h ON h.type = r.type
            WHERE r.target_id = @parent AND NOT (r.source_id = ANY(@path))
            GROUP BY r.source_id
        )
        SELECT v.id AS "Id", v.type AS "Type", v.title AS "Title", v.state AS "State", v.score AS "Score",
               edge.type AS "EdgeType", {ChildCountSql} AS "ChildCount"
        FROM child
        JOIN artifact v ON v.id = child.id AND {VisibleNode}
        JOIN hierarchy edge ON edge.precedence = child.precedence
        LEFT JOIN catalog c ON c.type = v.type
        ORDER BY child.precedence, c.position, v.title, v.id
        """;

    /// <summary>Nothing hangs from nothing: everything of project level without a parent lands here (rule 3).</summary>
    private const string UnclassifiedWhere = $"""
        WHERE {VisibleNode} AND v.type <> @moduleType
          AND NOT EXISTS (
              SELECT 1
              FROM relation r
              JOIN hierarchy h ON h.type = r.type
              JOIN artifact parent ON parent.id = r.target_id AND {VisibleParent}
              WHERE r.source_id = v.id)
        """;

    private const string UnclassifiedSql = $"""
        {Prelude}
        SELECT v.id AS "Id", v.type AS "Type", v.title AS "Title", v.state AS "State", v.score AS "Score",
               NULL::text AS "EdgeType", {ChildCountSql} AS "ChildCount"
        FROM artifact v
        LEFT JOIN catalog c ON c.type = v.type
        {UnclassifiedWhere}
        ORDER BY c.position, v.title, v.id
        """;

    private const string UnclassifiedCountSql = $"""
        {Prelude}
        SELECT count(*)::int AS "Value"
        FROM artifact v
        {UnclassifiedWhere}
        """;

    private const string MatchesWhere = $"""
            WHERE {VisibleNode}
              AND (@type IS NULL OR v.type = @type)
              AND (@state IS NULL OR v.state = @state)
              AND (@text IS NULL OR v.title ILIKE '%' || @text || '%')
        """;

    /// <summary>
    /// Filtered mode: from each match the walk climbs to the roots and the routes it finds are cut into their
    /// prefixes, so what comes back is every node of every branch that has a match in it — bounded by matches times
    /// depth, never by the size of the project. Every step reaches its neighbours through the relation indexes, and
    /// the two things a node needs to be drawn — its edge and how many children it has — are worked out once for the
    /// whole page and joined, not asked again for every row.
    /// </summary>
    private const string FilteredSql = $"""
        {RecursivePrelude},
        matches AS (
            SELECT v.id
            FROM artifact v
            LEFT JOIN catalog c ON c.type = v.type
        {MatchesWhere}
            ORDER BY c.position, v.title, v.id
            LIMIT @limit
        ),
        up AS (
            SELECT m.id AS node_id, ARRAY[m.id] AS path, 0 AS depth
            FROM matches m
          UNION
            SELECT parent.id, parent.id || u.path, u.depth + 1
            FROM up u
            JOIN relation r ON r.source_id = u.node_id
            JOIN hierarchy h ON h.type = r.type
            JOIN artifact parent ON parent.id = r.target_id AND {VisibleParent}
            WHERE u.depth < @maxDepth AND NOT (parent.id = ANY(u.path))
        ),
        -- «Does anybody hang above this route?» is answered for every route at once, by joining and folding: asked
        -- as a correlated EXISTS it is re-planned per route and the page of five hundred turns into five hundred
        -- little queries.
        topped AS (
            SELECT u.path, u.depth, top.type AS top_type,
                   COALESCE(bool_or(h.type IS NOT NULL AND parent.id IS NOT NULL), false) AS has_parent
            FROM up u
            LEFT JOIN artifact top ON top.id = u.path[1]
            LEFT JOIN relation r ON r.source_id = u.node_id
            LEFT JOIN hierarchy h ON h.type = r.type
            LEFT JOIN artifact parent ON parent.id = r.target_id AND {VisibleParent} AND NOT (parent.id = ANY(u.path))
            GROUP BY u.path, u.depth, top.type
        ),
        complete AS (
            -- A route is finished when it reaches a module (rule 1 makes every module a root), when nobody is left
            -- above it, or when the depth ceiling cut it (rule 5).
            SELECT path, top_type, has_parent FROM topped
            WHERE top_type = @moduleType OR NOT has_parent OR depth >= @maxDepth
        ),
        rooted AS (
            -- Rule 3 holds here too: a route whose top is neither a module nor cut short hangs from the synthetic
            -- bucket, so a match without a parent does not show up as a loose root and the two modes agree in shape.
            SELECT CASE
                       WHEN top_type = @moduleType OR has_parent THEN path
                       ELSE @unclassified || path
                   END AS path
            FROM complete
        ),
        node AS (
            SELECT DISTINCT
                   path[1:n] AS path,
                   path[n] AS node_id,
                   CASE WHEN n > 1 THEN path[n - 1] END AS parent_id
            FROM rooted, generate_subscripts(path, 1) AS n
        ),
        edged AS (
            SELECT n.path, MIN(h.precedence) AS precedence
            FROM node n
            JOIN relation r ON r.source_id = n.node_id AND r.target_id = n.parent_id
            JOIN hierarchy h ON h.type = r.type
            GROUP BY n.path
        ),
        counted AS (
            SELECT n.path, count(DISTINCT r.source_id)::int AS children
            FROM node n
            JOIN relation r ON r.target_id = n.node_id
            JOIN hierarchy h ON h.type = r.type
            JOIN artifact child ON child.id = r.source_id AND {VisibleChild}
            WHERE NOT (r.source_id = ANY(n.path))
            GROUP BY n.path
        )
        SELECT n.path AS "Path",
               n.node_id AS "Id",
               v.type AS "Type", v.title AS "Title", v.state AS "State", v.score AS "Score",
               edge.type AS "EdgeType",
               COALESCE(counted.children, 0) AS "ChildCount",
               (m.id IS NOT NULL) AS "Matches"
        FROM node n
        LEFT JOIN artifact v ON v.id = n.node_id
        LEFT JOIN edged ON edged.path = n.path
        LEFT JOIN hierarchy edge ON edge.precedence = edged.precedence
        LEFT JOIN counted ON counted.path = n.path
        LEFT JOIN catalog c ON c.type = v.type
        LEFT JOIN matches m ON m.id = n.node_id
        ORDER BY array_length(n.path, 1),
                 n.path[1:array_length(n.path, 1) - 1],
                 edged.precedence, c.position, v.title, v.id
        """;

    private const string MatchCountSql = $"""
        {Prelude}
        SELECT count(*)::int AS "Value"
        FROM artifact v
        {MatchesWhere}
        """;

    public async Task<IReadOnlyList<TreeRow>> GetRootAsync(TreeChildrenQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await ChildrenAsync(RootSql, query, Parameters(Common(query), [ModuleType(), Path(query)]), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TreeRow>> GetChildrenAsync(TreeChildrenQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var parameters = Parameters(Common(query), [Path(query), new NpgsqlParameter("parent", query.Path[^1])]);

        return await ChildrenAsync(ChildrenSql, query, parameters, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TreeRow>> GetUnclassifiedAsync(TreeChildrenQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await ChildrenAsync(UnclassifiedSql, query, Parameters(Common(query), [ModuleType(), Path(query)]), cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountUnclassifiedAsync(TreeChildrenQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var counted = await context.Database
            .SqlQueryRaw<CountRow>(UnclassifiedCountSql, Parameters(Common(query), [ModuleType()]))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return counted[0].Value;
    }

    public async Task<IReadOnlyList<TreeRow>> GetFilteredAsync(TreeFilterQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rows = await context.Database
            .SqlQueryRaw<FilteredRow>(
                FilteredSql,
                Parameters(
                    CommonOf(query.ProjectId),
                    [
                        ModuleType(),
                        Unclassified(),
                        Text(query.Text),
                        Type(query.Type),
                        State(query.State),
                        new NpgsqlParameter("maxDepth", query.MaxDepth),
                        new NpgsqlParameter("limit", query.MaxMatches),
                    ]))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new TreeRow(
                row.Path,
                row.Id,
                row.Type,
                row.Title,
                row.State is null ? null : EnumText<ArtifactState>.FromText(row.State),
                row.Score,
                row.EdgeType,
                row.ChildCount,
                row.Matches)),
        ];
    }

    public async Task<int> CountMatchesAsync(TreeFilterQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var counted = await context.Database
            .SqlQueryRaw<CountRow>(
                MatchCountSql,
                Parameters(CommonOf(query.ProjectId), [Text(query.Text), Type(query.Type), State(query.State)]))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return counted[0].Value;
    }

    private async Task<IReadOnlyList<TreeRow>> ChildrenAsync(
        string sql,
        TreeChildrenQuery query,
        NpgsqlParameter[] parameters,
        CancellationToken cancellationToken)
    {
        var rows = await context.Database
            .SqlQueryRaw<ChildRow>(sql, parameters)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new TreeRow(
                [.. query.Path, row.Id],
                row.Id,
                row.Type,
                row.Title,
                EnumText<ArtifactState>.FromText(row.State),
                row.Score,
                row.EdgeType,
                row.ChildCount,
                Matches: false)),
        ];
    }

    /// <summary>
    /// The three every query needs: the hierarchical types and the catalog order straight from the single place they
    /// live in, and the project. Passing the lists instead of naming them in the SQL is what keeps the order of the
    /// C# and the order of the query the same thing.
    /// </summary>
    private static NpgsqlParameter[] CommonOf(Guid projectId) =>
    [
        new("types", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = HierarchyRelations.ByPrecedence.ToArray() },
        new("catalog", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = ArtifactTypeCatalog.InOrder.ToArray() },
        new("project", projectId),
    ];

    private static NpgsqlParameter[] Common(TreeChildrenQuery query) => CommonOf(query.ProjectId);

    private static NpgsqlParameter Path(TreeChildrenQuery query) =>
        new("path", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = query.Path.ToArray() };

    private static NpgsqlParameter ModuleType() => new("moduleType", ArtifactTypeCatalog.Module);

    private static NpgsqlParameter Unclassified() =>
        new("unclassified", NpgsqlDbType.Uuid) { Value = TreeNodeKinds.UnclassifiedId };

    /// <summary>Joins the groups above into the flat array the raw query takes.</summary>
    private static NpgsqlParameter[] Parameters(params IEnumerable<NpgsqlParameter>[] groups) =>
        [.. groups.SelectMany(group => group)];

    private static NpgsqlParameter Type(string? type) =>
        new("type", NpgsqlDbType.Text) { Value = (object?)type ?? DBNull.Value };

    private static NpgsqlParameter State(ArtifactState? state) =>
        new("state", NpgsqlDbType.Text) { Value = state is null ? DBNull.Value : EnumText<ArtifactState>.ToText(state.Value) };

    private static NpgsqlParameter Text(string? text) =>
        new("text", NpgsqlDbType.Text) { Value = (object?)text ?? DBNull.Value };

    private sealed record ChildRow(Guid Id, string Type, string Title, string State, int? Score, string? EdgeType, int ChildCount);

    private sealed record FilteredRow(
        Guid[] Path,
        Guid Id,
        string? Type,
        string? Title,
        string? State,
        int? Score,
        string? EdgeType,
        int ChildCount,
        bool Matches);

    private sealed record CountRow(int Value);
}
