using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Application.Tests.Traceability.Fakes;

/// <summary>In-memory decision log. The paging and the joins belong to the database, so the integration tests cover
/// those; here the fake answers only what the service reasons about.</summary>
internal sealed class FakeDecisionRepository : IDecisionRepository
{
    public List<Decision> Decisions { get; } = [];

    public List<DecisionArtifact> Artifacts { get; } = [];

    public List<DecisionAuthorRole> AuthorRoles { get; } = [];

    public List<DecisionCompetentRole> CompetentRoles { get; } = [];

    public List<ArtifactTypeRole> CompetenceMap { get; } = [];

    public void Add(Decision decision) => Decisions.Add(decision);

    public void AddArtifact(DecisionArtifact link) => Artifacts.Add(link);

    public void AddAuthorRole(DecisionAuthorRole authorRole) => AuthorRoles.Add(authorRole);

    public void AddCompetentRole(DecisionCompetentRole competentRole) => CompetentRoles.Add(competentRole);

    public Task<Decision?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Decisions.SingleOrDefault(decision => decision.Id == id));

    public Task<IReadOnlyList<Guid>> GetArtifactIdsAsync(Guid decisionId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>([.. Artifacts.Where(link => link.DecisionId == decisionId).Select(link => link.ArtifactId)]);

    public Task<IReadOnlyList<Role>> GetCompetentRolesAsync(Guid decisionId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Role>>([.. CompetentRoles.Where(role => role.DecisionId == decisionId).Select(role => role.Role)]);

    public Task<IReadOnlyList<DecisionRecord>> SearchAsync(DecisionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var found = Decisions
            .Where(decision => decision.ProjectId == query.ProjectId)
            .Where(decision => query.ArtifactId is not { } artifactId
                || Artifacts.Any(link => link.DecisionId == decision.Id && link.ArtifactId == artifactId))
            .Where(decision => query.PendingRole is not { } role
                || (decision.State == DecisionState.Pending
                    && CompetentRoles.Any(competent => competent.DecisionId == decision.Id && competent.Role == role)))
            .OrderByDescending(decision => decision.CreatedAt)
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(decision => new DecisionRecord(
                decision.Id,
                decision.ProjectId,
                decision.Type,
                decision.State,
                decision.AuthorType,
                decision.AuthorId,
                decision.Justification,
                decision.ParentDecisionId,
                decision.CreatedAt,
                [.. Artifacts.Where(link => link.DecisionId == decision.Id).Select(link => link.ArtifactId)],
                [.. AuthorRoles.Where(role => role.DecisionId == decision.Id).Select(role => role.Role).Order()],
                [.. CompetentRoles.Where(role => role.DecisionId == decision.Id).Select(role => role.Role).Order()]))
            .ToList();

        return Task.FromResult<IReadOnlyList<DecisionRecord>>(found);
    }

    public Task<int> CountAsync(DecisionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Task.FromResult(Decisions.Count(decision => decision.ProjectId == query.ProjectId));
    }

    public Task<IReadOnlyList<ArtifactTypeRole>> GetCompetenceMapAsync(IReadOnlyCollection<string> artifactTypes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactTypes);

        return Task.FromResult<IReadOnlyList<ArtifactTypeRole>>(
            [.. CompetenceMap.Where(entry => artifactTypes.Contains(entry.ArtifactType, StringComparer.Ordinal))]);
    }

    public Task<IReadOnlyList<ArtifactTypeRole>> GetCompetenceMapAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ArtifactTypeRole>>([.. CompetenceMap]);
}
