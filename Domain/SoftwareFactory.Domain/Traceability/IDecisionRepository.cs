using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Traceability;

/// <summary>
/// The decision log of the current tenant (HU-003). Methods carry intention and never an IQueryable
/// (estandar-backend.md §2); row-level security does the tenant filtering.
/// </summary>
public interface IDecisionRepository
{
    void Add(Decision decision);

    void AddArtifact(DecisionArtifact link);

    void AddAuthorRole(DecisionAuthorRole authorRole);

    void AddCompetentRole(DecisionCompetentRole competentRole);

    /// <summary>Decision with its current state, tracked so it can be settled.</summary>
    Task<Decision?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Artifacts the decision bears on.</summary>
    Task<IReadOnlyList<Guid>> GetArtifactIdsAsync(Guid decisionId, CancellationToken cancellationToken);

    /// <summary>Roles that may settle the decision, as they were stored when it was recorded (HU-003 §2).</summary>
    Task<IReadOnlyList<Role>> GetCompetentRolesAsync(Guid decisionId, CancellationToken cancellationToken);

    /// <summary>A page of decisions with their artifacts and their snapshot, newest first (HU-003 §5).</summary>
    Task<IReadOnlyList<DecisionRecord>> SearchAsync(DecisionQuery query, CancellationToken cancellationToken);

    Task<int> CountAsync(DecisionQuery query, CancellationToken cancellationToken);

    /// <summary>The competence map in force for the tenant, restricted to the types asked about (HU-003 §2).</summary>
    Task<IReadOnlyList<ArtifactTypeRole>> GetCompetenceMapAsync(IReadOnlyCollection<string> artifactTypes, CancellationToken cancellationToken);

    /// <summary>The whole competence map of the tenant, for the read endpoint the artifact card consumes.</summary>
    Task<IReadOnlyList<ArtifactTypeRole>> GetCompetenceMapAsync(CancellationToken cancellationToken);
}

/// <summary>Filters of the decision list (HU-003 §5).</summary>
public sealed record DecisionQuery(Guid ProjectId)
{
    /// <summary>Only decisions bearing on this artifact.</summary>
    public Guid? ArtifactId { get; init; }

    /// <summary>
    /// Only pending notes this role may settle. It reads the stored snapshot; it never recomputes the map, so a note
    /// keeps the owners it was born with.
    /// </summary>
    public Role? PendingRole { get; init; }

    public int Skip { get; init; }

    public int Take { get; init; } = 50;
}

/// <summary>A decision as the log reads it: the entry, what it bears on, the hats it was written with, and who may settle it.</summary>
public sealed record DecisionRecord(
    Guid Id,
    Guid ProjectId,
    DecisionType Type,
    DecisionState State,
    AuthorType AuthorType,
    Guid AuthorId,
    string Justification,
    Guid? ParentDecisionId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<Guid> ArtifactIds,
    IReadOnlyList<Role> AuthorRoles,
    IReadOnlyList<Role> CompetentRoles);
