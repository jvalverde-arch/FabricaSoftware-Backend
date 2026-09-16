using Microsoft.EntityFrameworkCore;
using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Infrastructure.Persistence.Repositories;

/// <summary>
/// Artifacts of the current tenant (HU-001). Row-level security does the tenant filtering, so these queries only
/// express intent; the module filter walks the <c>belongs_to</c> relation instead of duplicating it as a column.
/// </summary>
public sealed class ArtifactRepository(SoftwareFactoryDbContext context) : IArtifactRepository
{
    private const string BelongsTo = "belongs_to";

    public void Add(Artifact artifact) => context.Artifacts.Add(artifact);

    public void AddVersion(ArtifactVersion version) => context.ArtifactVersions.Add(version);

    public Task<Artifact?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Artifacts.SingleOrDefaultAsync(artifact => artifact.Id == id && artifact.DeletedAt == null, cancellationToken);

    public Task<ArtifactVersion?> GetVersionAsync(Guid artifactId, int number, CancellationToken cancellationToken) =>
        context.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(version => version.ArtifactId == artifactId && version.Number == number, cancellationToken);

    public async Task<IReadOnlyList<ArtifactVersion>> GetVersionsAsync(Guid artifactId, CancellationToken cancellationToken) =>
        await context.ArtifactVersions
            .AsNoTracking()
            .Where(version => version.ArtifactId == artifactId)
            .OrderBy(version => version.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Artifact>> SearchAsync(ArtifactQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await Filter(query)
            .OrderByDescending(artifact => artifact.UpdatedAt)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<int> CountAsync(ArtifactQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Filter(query).CountAsync(cancellationToken);
    }

    /// <summary>
    /// Both directions are asked separately: EF cannot union two queries that already project into a record, and
    /// this runs only when someone tries to delete, so two small reads are cheaper than a clever single one.
    /// </summary>
    public async Task<IReadOnlyList<ArtifactRelationReference>> GetActiveRelationsAsync(Guid artifactId, CancellationToken cancellationToken)
    {
        var outgoing = await (
            from relation in context.Relations.AsNoTracking()
            join target in context.Artifacts on relation.TargetId equals target.Id
            where relation.SourceId == artifactId && target.DeletedAt == null
            select new ArtifactRelationReference(relation.Id, relation.Type, target.Id, target.Title, false))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var incoming = await (
            from relation in context.Relations.AsNoTracking()
            join source in context.Artifacts on relation.SourceId equals source.Id
            where relation.TargetId == artifactId && source.DeletedAt == null
            select new ArtifactRelationReference(relation.Id, relation.Type, source.Id, source.Title, true))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. outgoing, .. incoming];
    }

    private IQueryable<Artifact> Filter(ArtifactQuery query)
    {
        var artifacts = context.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.ProjectId == query.ProjectId && artifact.DeletedAt == null);

        if (query.Type is { Length: > 0 } type)
        {
            artifacts = artifacts.Where(artifact => artifact.Type == type);
        }

        if (query.State is { } state)
        {
            artifacts = artifacts.Where(artifact => artifact.State == state);
        }

        if (query.MinScore is { } min)
        {
            artifacts = artifacts.Where(artifact => artifact.Score >= min);
        }

        if (query.MaxScore is { } max)
        {
            artifacts = artifacts.Where(artifact => artifact.Score <= max);
        }

        if (query.Title is { Length: > 0 } title)
        {
            artifacts = artifacts.Where(artifact => EF.Functions.ILike(artifact.Title, $"%{title}%"));
        }

        if (query.ModuleId is { } moduleId)
        {
            artifacts = artifacts.Where(artifact => context.Relations.Any(relation =>
                relation.SourceId == artifact.Id && relation.TargetId == moduleId && relation.Type == BelongsTo));
        }

        return artifacts;
    }
}
