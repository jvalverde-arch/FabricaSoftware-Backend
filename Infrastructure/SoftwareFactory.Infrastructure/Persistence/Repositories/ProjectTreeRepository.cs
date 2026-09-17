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
    /// Shared head of every query. <c>visible</c> is the tree's universe — this project, project level, alive — which
    /// is also what keeps the tenant's global artifacts out of it (hierarchy rule 6). <c>edge</c> collapses the
    /// relations between a pair to the single winning edge: when two artifacts are joined by more than one
    /// hierarchical type, the lowest ordinal wins, which is the precedence of rule 2.
    /// </summary>
    private const string PreludeBody = """
        hierarchy AS (
            SELECT type, ordinality AS precedence FROM unnest(@types) WITH ORDINALITY AS t(type, ordinality)
        ),
        catalog AS (
            SELECT type, ordinality AS position FROM unnest(@catalog) WITH ORDINALITY AS c(type, ordinality)
        ),
        visible AS (
            SELECT a.id, a.type, a.title, a.state, a.score
            FROM artifact a
            WHERE a.project_id = @project AND a.level = 'project' AND a.deleted_at IS NULL
        ),
        edge AS (
            SELECT r.source_id AS child_id, r.target_id AS parent_id, MIN(h.precedence) AS precedence
            FROM relation r
            JOIN hierarchy h ON h.type = r.type
            GROUP BY r.source_id, r.target_id
        )
        """;

    private const string Prelude = $"WITH {PreludeBody}";

    /// <summary>The same head, for the one query that walks upward: a self-referencing CTE needs the whole WITH to
    /// be declared recursive, not just its own branch.</summary>
    private const string RecursivePrelude = $"WITH RECURSIVE {PreludeBody}";

    /// <summary>
    /// How many children a node would show if it were expanded. It takes the node's own route so the count never
    /// includes an ancestor: promising a child that the walk will refuse to follow (rule 5) shows a chevron that
    /// expands into nothing.
    /// </summary>
    private const string ChildCountSql = """
        (SELECT count(*)::int
         FROM edge inner_edge
         JOIN visible inner_child ON inner_child.id = inner_edge.child_id
         WHERE inner_edge.parent_id = v.id AND NOT (inner_child.id = ANY(@path || v.id)))
        """;

    private const string RootSql = $"""
        {Prelude}
        SELECT v.id AS "Id", v.type AS "Type", v.title AS "Title", v.state AS "State", v.score AS "Score",
               NULL::text AS "EdgeType", {ChildCountSql} AS "ChildCount"
        FROM visible v
        LEFT JOIN catalog c ON c.type = v.type
        WHERE v.type = @moduleType
        ORDER BY c.position, v.title, v.id
        """;

    private const string ChildrenSql = $"""
        {Prelude}
        SELECT v.id AS "Id", v.type AS "Type", v.title AS "Title", v.state AS "State", v.score AS "Score",
               (SELECT h.type FROM hierarchy h WHERE h.precedence = e.precedence) AS "EdgeType",
               {ChildCountSql} AS "ChildCount"
        FROM edge e
        JOIN visible v ON v.id = e.child_id
        LEFT JOIN catalog c ON c.type = v.type
        WHERE e.parent_id = @parent AND NOT (v.id = ANY(@path))
        ORDER BY e.precedence, c.position, v.title, v.id
        """;

    /// <summary>Nothing hangs from nothing: everything of project level without a parent lands here (rule 3).</summary>
    private const string UnclassifiedWhere = """
        WHERE v.type <> @moduleType
          AND NOT EXISTS (
              SELECT 1 FROM edge parent_edge
              JOIN visible parent ON parent.id = parent_edge.parent_id
              WHERE parent_edge.child_id = v.id)
        """;

    private const string UnclassifiedSql = $"""
        {Prelude}
        SELECT v.id AS "Id", v.type AS "Type", v.title AS "Title", v.state AS "State", v.score AS "Score",
               NULL::text AS "EdgeType", {ChildCountSql} AS "ChildCount"
        FROM visible v
        LEFT JOIN catalog c ON c.type = v.type
        {UnclassifiedWhere}
        ORDER BY c.position, v.title, v.id
        """;

    private const string UnclassifiedCountSql = $"""
        {Prelude}
        SELECT count(*)::int AS "Value"
        FROM visible v
        {UnclassifiedWhere}
        """;

    private const string MatchesWhere = """
            WHERE (@type IS NULL OR v.type = @type)
              AND (@state IS NULL OR v.state = @state)
              AND (@text IS NULL OR v.title ILIKE '%' || @text || '%')
        """;

    /// <summary>
    /// Filtered mode: from each match the walk climbs to the roots and the routes it finds are cut into their
    /// prefixes, so what comes back is every node of every branch that has a match in it — bounded by matches times
    /// depth, never by the size of the project.
    /// </summary>
    private const string FilteredSql = $"""
        {RecursivePrelude},
        matches AS (
            SELECT v.id
            FROM visible v
            LEFT JOIN catalog c ON c.type = v.type
        {MatchesWhere}
            ORDER BY c.position, v.title, v.id
            LIMIT @limit
        ),
        up AS (
            SELECT m.id AS node_id, ARRAY[m.id] AS path, 0 AS depth
            FROM matches m
          UNION ALL
            SELECT parent.id, parent.id || u.path, u.depth + 1
            FROM up u
            JOIN edge e ON e.child_id = u.node_id
            JOIN visible parent ON parent.id = e.parent_id
            WHERE u.depth < @maxDepth AND NOT (parent.id = ANY(u.path))
        ),
        topped AS (
            SELECT u.path,
                   (SELECT v.type FROM visible v WHERE v.id = u.path[1]) AS top_type,
                   EXISTS (
                       SELECT 1 FROM edge e
                       JOIN visible parent ON parent.id = e.parent_id
                       WHERE e.child_id = u.node_id AND NOT (parent.id = ANY(u.path))) AS has_parent,
                   u.depth
            FROM up u
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
        prefix AS (
            SELECT DISTINCT path[1:n] AS path FROM rooted, generate_subscripts(path, 1) AS n
        )
        SELECT p.path AS "Path",
               p.path[array_length(p.path, 1)] AS "Id",
               v.type AS "Type", v.title AS "Title", v.state AS "State", v.score AS "Score",
               (SELECT h.type FROM hierarchy h
                WHERE h.precedence = (SELECT e.precedence FROM edge e
                                      WHERE e.child_id = p.path[array_length(p.path, 1)]
                                        AND e.parent_id = p.path[array_length(p.path, 1) - 1])) AS "EdgeType",
               COALESCE((SELECT count(*)::int
                         FROM edge inner_edge
                         JOIN visible inner_child ON inner_child.id = inner_edge.child_id
                         WHERE inner_edge.parent_id = v.id AND NOT (inner_child.id = ANY(p.path))), 0) AS "ChildCount",
               EXISTS (SELECT 1 FROM matches m WHERE m.id = v.id) AS "Matches"
        FROM prefix p
        LEFT JOIN visible v ON v.id = p.path[array_length(p.path, 1)]
        LEFT JOIN catalog c ON c.type = v.type
        ORDER BY array_length(p.path, 1),
                 p.path[1:array_length(p.path, 1) - 1],
                 (SELECT e.precedence FROM edge e
                  WHERE e.child_id = p.path[array_length(p.path, 1)]
                    AND e.parent_id = p.path[array_length(p.path, 1) - 1]),
                 c.position, v.title, v.id
        """;

    private const string MatchCountSql = $"""
        {Prelude}
        SELECT count(*)::int AS "Value"
        FROM visible v
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
