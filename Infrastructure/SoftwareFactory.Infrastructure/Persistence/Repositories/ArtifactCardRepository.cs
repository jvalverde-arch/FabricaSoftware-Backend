using Microsoft.EntityFrameworkCore;
using Npgsql;
using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Persistence.Repositories;

/// <summary>
/// The relation panel of the card (HU-005 §2) in one statement. Row-level security does the tenant filtering; what
/// this adds over the plain relation read is the other end's project and module, which is what lets the card say
/// «this leaves the project» before somebody clicks and lands somewhere else.
/// </summary>
public sealed class ArtifactCardRepository(SoftwareFactoryDbContext context) : IArtifactCardRepository
{
    /// <summary>
    /// The module of an artifact is the target of its <c>belongs_to</c>; a module is its own module, which is what
    /// makes «same module» true between a module and the story hanging from it.
    /// </summary>
    private const string Sql = """
        WITH relevant AS (
            SELECT r.id, r.type, true AS upstream, r.target_id AS other_id
            FROM relation r
            WHERE r.source_id = @artifact
          UNION ALL
            SELECT r.id, r.type, false AS upstream, r.source_id AS other_id
            FROM relation r
            WHERE r.target_id = @artifact
        )
        SELECT relevant.id AS "RelationId",
               relevant.type AS "RelationType",
               relevant.upstream AS "Upstream",
               other.id AS "OtherId",
               other.type AS "OtherType",
               other.title AS "OtherTitle",
               other.state AS "OtherState",
               other.level AS "OtherLevel",
               other.score AS "OtherScore",
               other.project_id AS "OtherProjectId",
               project.name AS "OtherProjectName",
               module.id AS "OtherModuleId",
               module.title AS "OtherModuleTitle"
        FROM relevant
        JOIN artifact other ON other.id = relevant.other_id AND other.deleted_at IS NULL
        JOIN project ON project.id = other.project_id
        LEFT JOIN LATERAL (
            SELECT parent.id, parent.title
            FROM relation belongs
            JOIN artifact parent ON parent.id = belongs.target_id AND parent.type = 'module' AND parent.deleted_at IS NULL
            WHERE belongs.source_id = other.id AND belongs.type = 'belongs_to'
            ORDER BY parent.title, parent.id
            LIMIT 1
        ) AS module ON other.type <> 'module'
        ORDER BY relevant.upstream DESC, relevant.type, other.title, other.id
        """;

    public async Task<IReadOnlyList<CardRelationRow>> GetRelationsAsync(Guid artifactId, CancellationToken cancellationToken)
    {
        var rows = await context.Database
            .SqlQueryRaw<RelationRow>(Sql, new NpgsqlParameter("artifact", artifactId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new CardRelationRow(
                row.RelationId,
                row.RelationType,
                row.Upstream,
                row.OtherId,
                row.OtherType,
                row.OtherTitle,
                EnumText<ArtifactState>.FromText(row.OtherState),
                EnumText<ArtifactLevel>.FromText(row.OtherLevel),
                row.OtherScore,
                row.OtherProjectId,
                row.OtherProjectName,
                // A module is its own module: that is what makes the story under it «same module» and not a crossing.
                string.Equals(row.OtherType, ArtifactTypeCatalog.Module, StringComparison.Ordinal) ? row.OtherId : row.OtherModuleId,
                string.Equals(row.OtherType, ArtifactTypeCatalog.Module, StringComparison.Ordinal) ? row.OtherTitle : row.OtherModuleTitle)),
        ];
    }

    private sealed record RelationRow(
        Guid RelationId,
        string RelationType,
        bool Upstream,
        Guid OtherId,
        string OtherType,
        string OtherTitle,
        string OtherState,
        string OtherLevel,
        int? OtherScore,
        Guid OtherProjectId,
        string OtherProjectName,
        Guid? OtherModuleId,
        string? OtherModuleTitle);
}
