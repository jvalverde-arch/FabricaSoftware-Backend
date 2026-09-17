using Microsoft.EntityFrameworkCore;
using Npgsql;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Repositories;

/// <summary>
/// Relations of the current tenant (HU-002). Row-level security does the tenant filtering; the neighborhood walk is
/// raw SQL because a recursive CTE is the only way to answer «everything within N hops» in one round trip.
/// </summary>
public sealed class RelationRepository(SoftwareFactoryDbContext context) : IRelationRepository
{
    /// <summary>
    /// Walk of the graph in both directions, bounded by depth. <c>UNION</c> (not <c>UNION ALL</c>) folds the repeated
    /// pairs, and the depth bound is what makes a cycle terminate instead of spinning: a node already seen at a
    /// shallower depth contributes nothing new.
    /// </summary>
    private const string WalkSql = """
        WITH RECURSIVE walk AS (
            SELECT root.id, 0 AS depth
            FROM artifact root
            WHERE root.id = @root AND root.deleted_at IS NULL
          UNION
            SELECT other.id, walk.depth + 1
            FROM walk
            JOIN relation ON relation.source_id = walk.id OR relation.target_id = walk.id
            JOIN artifact other
              ON other.id = CASE WHEN relation.source_id = walk.id THEN relation.target_id ELSE relation.source_id END
            WHERE walk.depth < @levels AND other.deleted_at IS NULL
        )
        """;

    private const string NodesSql = $"""
        {WalkSql}
        , reached AS (SELECT id, MIN(depth) AS depth FROM walk GROUP BY id)
        SELECT artifact.id AS "Id",
               artifact.project_id AS "ProjectId",
               artifact.type AS "Type",
               artifact.title AS "Title",
               artifact.state AS "State",
               artifact.level AS "Level",
               artifact.score AS "Score",
               reached.depth AS "Depth"
        FROM reached
        JOIN artifact ON artifact.id = reached.id
        ORDER BY reached.depth, artifact.title
        """;

    // Every relation between two artifacts that the walk reached, including the ones joining two nodes of the last
    // level: the neighborhood is a subgraph, and hiding the edge between two nodes it already returned would lie.
    private const string LinksSql = $"""
        {WalkSql}
        , reached AS (SELECT DISTINCT id FROM walk)
        SELECT relation.id AS "Id",
               relation.source_id AS "SourceId",
               relation.target_id AS "TargetId",
               relation.type AS "Type"
        FROM relation
        WHERE relation.source_id IN (SELECT id FROM reached)
          AND relation.target_id IN (SELECT id FROM reached)
        """;

    public void Add(Relation relation) => context.Relations.Add(relation);

    public void Remove(Relation relation) => context.Relations.Remove(relation);

    public Task<Relation?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Relations.SingleOrDefaultAsync(relation => relation.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid sourceId, Guid targetId, string type, CancellationToken cancellationToken) =>
        context.Relations.AnyAsync(
            relation => relation.SourceId == sourceId && relation.TargetId == targetId && relation.Type == type,
            cancellationToken);

    public async Task<IReadOnlyList<Relation>> GetForArtifactAsync(Guid artifactId, CancellationToken cancellationToken) =>
        await context.Relations
            .AsNoTracking()
            .Where(relation => relation.SourceId == artifactId || relation.TargetId == artifactId)
            .OrderByDescending(relation => relation.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<ArtifactNeighborhood> GetNeighborhoodAsync(Guid rootId, int levels, CancellationToken cancellationToken)
    {
        // Two statements, one walk each: the nodes carry their depth and the links only join what was reached. Asking
        // for both shapes in a single result set would mean a union of unrelated columns, which is harder to read
        // than running the CTE twice on an index-backed graph.
        var nodes = await context.Database
            .SqlQueryRaw<NodeRow>(NodesSql, Root(rootId), Levels(levels))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var links = await context.Database
            .SqlQueryRaw<LinkRow>(LinksSql, Root(rootId), Levels(levels))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ArtifactNeighborhood(
            [.. nodes.Select(node => new NeighborhoodHop(
                node.Id,
                node.ProjectId,
                node.Type,
                node.Title,
                EnumText<ArtifactState>.FromText(node.State),
                EnumText<ArtifactLevel>.FromText(node.Level),
                node.Score,
                node.Depth))],
            [.. links.Select(link => new NeighborhoodLink(link.Id, link.SourceId, link.TargetId, link.Type))]);
    }

    public async Task<IReadOnlyList<Artifact>> GetOrphansAsync(Guid projectId, string type, string missingRelation, CancellationToken cancellationToken) =>
        await context.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.ProjectId == projectId && artifact.Type == type && artifact.DeletedAt == null)
            // Either end counts: a user_story is covered whether it validates something or something validates it.
            .Where(artifact => !context.Relations.Any(relation =>
                relation.Type == missingRelation && (relation.SourceId == artifact.Id || relation.TargetId == artifact.Id)))
            .OrderBy(artifact => artifact.Title)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private static NpgsqlParameter Root(Guid rootId) => new("root", rootId);

    private static NpgsqlParameter Levels(int levels) => new("levels", levels);

    private sealed record NodeRow(Guid Id, Guid ProjectId, string Type, string Title, string State, string Level, int? Score, int Depth);

    private sealed record LinkRow(Guid Id, Guid SourceId, Guid TargetId, string Type);
}
