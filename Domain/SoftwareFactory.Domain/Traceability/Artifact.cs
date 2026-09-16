using System.Collections.Frozen;
using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Traceability;

/// <summary>
/// Node of the traceability model (doc 03 §3, HU-001). The content lives in immutable versions; the type comes from
/// the configurable catalog. Its life cycle is draft → in_review → approved → frozen, with the way back allowed while
/// it is not frozen: a frozen artifact is a closed agreement and only a new artifact supersedes it.
/// </summary>
public sealed class Artifact : TenantScopedEntity
{
    public const int MinScore = 0;
    public const int MaxScore = 100;

    private static readonly FrozenDictionary<ArtifactState, ArtifactState[]> _transitions = new Dictionary<ArtifactState, ArtifactState[]>
    {
        [ArtifactState.Draft] = [ArtifactState.InReview],
        [ArtifactState.InReview] = [ArtifactState.Approved, ArtifactState.Draft],
        [ArtifactState.Approved] = [ArtifactState.Frozen, ArtifactState.Draft],
        [ArtifactState.Frozen] = [],
    }.ToFrozenDictionary();

    private Artifact()
    {
    }

    public Artifact(Guid tenantId, Guid projectId, string type, string title, ArtifactLevel level)
        : base(tenantId)
    {
        Guard.NotEmpty(projectId);
        ProjectId = projectId;
        Type = Guard.NotBlank(type);
        Title = Guard.NotBlank(title);
        Level = level;
        State = ArtifactState.Draft;
        CurrentVersion = 0;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid ProjectId { get; private set; }

    /// <summary>Artifact type code from the catalog (user_story, business_rule, screen, boundary_contract, ...).</summary>
    public string Type { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public ArtifactState State { get; private set; }

    /// <summary>Completeness score 0-100; null until an evaluator has scored the artifact (S5 computes it).</summary>
    public int? Score { get; private set; }

    public ArtifactLevel Level { get; private set; }

    /// <summary>Number of the latest <see cref="ArtifactVersion"/>; 0 while no content has been written.</summary>
    public int CurrentVersion { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    /// <summary>False once the artifact is frozen or deleted: neither accepts new content.</summary>
    public bool IsEditable => !IsDeleted && State != ArtifactState.Frozen;

    /// <summary>True when the life cycle allows moving to <paramref name="state"/> right now.</summary>
    public bool CanChangeStateTo(ArtifactState state) =>
        !IsDeleted && (state == State || _transitions[State].Contains(state));

    public void ChangeState(ArtifactState state, DateTimeOffset now)
    {
        RequireAlive();

        if (state == State)
        {
            return;
        }

        if (!_transitions[State].Contains(state))
        {
            throw new InvalidOperationException($"An artifact in {State} cannot move to {state}.");
        }

        State = state;
        Touch(now);
    }

    public void Rename(string title, DateTimeOffset now)
    {
        RequireEditable();
        Title = Guard.NotBlank(title);
        Touch(now);
    }

    /// <summary>Registers that a new version was written and returns its number.</summary>
    public int AdvanceVersion(DateTimeOffset now)
    {
        RequireEditable();
        CurrentVersion++;
        Touch(now);
        return CurrentVersion;
    }

    public void SetScore(int? score, DateTimeOffset now)
    {
        RequireAlive();

        if (score is { } value)
        {
            Guard.InRange(value, MinScore, MaxScore);
        }

        Score = score;
        Touch(now);
    }

    /// <summary>Logical delete (HU-001 §4); the caller checks first that no active relation points at it.</summary>
    public void Delete(DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return;
        }

        DeletedAt = now.ToUniversalTime();
        Touch(now);
    }

    private void RequireAlive()
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted artifact cannot be modified.");
        }
    }

    private void RequireEditable()
    {
        RequireAlive();

        if (State == ArtifactState.Frozen)
        {
            throw new InvalidOperationException("A frozen artifact cannot be modified; supersede it with a new one.");
        }
    }

    private void Touch(DateTimeOffset now) => UpdatedAt = now.ToUniversalTime();
}
