using Microsoft.EntityFrameworkCore;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Infrastructure.Persistence.Repositories;

/// <summary>
/// The decision log of the current tenant (HU-003). Row-level security does the tenant filtering, so these queries
/// only express intent. «Pending notes of my role» reads the stored snapshot and never the competence map: a note
/// keeps the owners it was born with even after the map is edited.
/// </summary>
public sealed class DecisionRepository(SoftwareFactoryDbContext context) : IDecisionRepository
{
    public void Add(Decision decision) => context.Decisions.Add(decision);

    public void AddArtifact(DecisionArtifact link) => context.DecisionArtifacts.Add(link);

    public void AddAuthorRole(DecisionAuthorRole authorRole) => context.DecisionAuthorRoles.Add(authorRole);

    public void AddCompetentRole(DecisionCompetentRole competentRole) => context.DecisionCompetentRoles.Add(competentRole);

    public Task<Decision?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Decisions.SingleOrDefaultAsync(decision => decision.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetArtifactIdsAsync(Guid decisionId, CancellationToken cancellationToken) =>
        await context.DecisionArtifacts
            .AsNoTracking()
            .Where(link => link.DecisionId == decisionId)
            .Select(link => link.ArtifactId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Role>> GetCompetentRolesAsync(Guid decisionId, CancellationToken cancellationToken) =>
        await context.DecisionCompetentRoles
            .AsNoTracking()
            .Where(competent => competent.DecisionId == decisionId)
            .Select(competent => competent.Role)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<DecisionRecord>> SearchAsync(DecisionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = await Filter(query)
            .OrderByDescending(decision => decision.CreatedAt)
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(decision => new
            {
                decision.Id,
                decision.ProjectId,
                decision.Type,
                decision.State,
                decision.AuthorType,
                decision.AuthorId,
                decision.Justification,
                decision.ParentDecisionId,
                decision.CreatedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (page.Count == 0)
        {
            return [];
        }

        // One extra round trip each for the artifacts and the two snapshots of the whole page, instead of one per row.
        var ids = page.ConvertAll(decision => decision.Id);

        var artifacts = (await context.DecisionArtifacts
                .AsNoTracking()
                .Where(link => ids.Contains(link.DecisionId))
                .Select(link => new { link.DecisionId, link.ArtifactId })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .GroupBy(link => link.DecisionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<Guid>)[.. group.Select(link => link.ArtifactId)]);

        var authored = (await context.DecisionAuthorRoles
                .AsNoTracking()
                .Where(role => ids.Contains(role.DecisionId))
                .Select(role => new { role.DecisionId, role.Role })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .GroupBy(role => role.DecisionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<Role>)[.. group.Select(role => role.Role).Order()]);

        var competent = (await context.DecisionCompetentRoles
                .AsNoTracking()
                .Where(role => ids.Contains(role.DecisionId))
                .Select(role => new { role.DecisionId, role.Role })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .GroupBy(role => role.DecisionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<Role>)[.. group.Select(role => role.Role).Order()]);

        return [.. page.Select(decision => new DecisionRecord(
            decision.Id,
            decision.ProjectId,
            decision.Type,
            decision.State,
            decision.AuthorType,
            decision.AuthorId,
            decision.Justification,
            decision.ParentDecisionId,
            decision.CreatedAt,
            artifacts.TryGetValue(decision.Id, out var touched) ? touched : [],
            authored.TryGetValue(decision.Id, out var hats) ? hats : [],
            competent.TryGetValue(decision.Id, out var roles) ? roles : []))];
    }

    public Task<int> CountAsync(DecisionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Filter(query).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArtifactTypeRole>> GetCompetenceMapAsync(IReadOnlyCollection<string> artifactTypes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactTypes);

        return await context.ArtifactTypeRoles
            .AsNoTracking()
            .Where(map => artifactTypes.Contains(map.ArtifactType))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ArtifactTypeRole>> GetCompetenceMapAsync(CancellationToken cancellationToken) =>
        await context.ArtifactTypeRoles
            .AsNoTracking()
            .OrderBy(map => map.ArtifactType)
            .ThenBy(map => map.Role)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private IQueryable<Decision> Filter(DecisionQuery query)
    {
        var decisions = context.Decisions.AsNoTracking().Where(decision => decision.ProjectId == query.ProjectId);

        if (query.ArtifactId is { } artifactId)
        {
            decisions = decisions.Where(decision =>
                context.DecisionArtifacts.Any(link => link.DecisionId == decision.Id && link.ArtifactId == artifactId));
        }

        if (query.PendingRole is { } role)
        {
            decisions = decisions.Where(decision =>
                decision.State == DecisionState.Pending
                && context.DecisionCompetentRoles.Any(competent => competent.DecisionId == decision.Id && competent.Role == role));
        }

        return decisions;
    }
}
