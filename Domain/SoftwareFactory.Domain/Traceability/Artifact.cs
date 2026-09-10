using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Traceability;

/// <summary>Node of the traceability model (doc 03 §3). Content lives in versions; the type comes from the configurable catalog.</summary>
public sealed class Artifact : TenantScopedEntity
{
    public const int MinScore = 0;
    public const int MaxScore = 100;

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

    /// <summary>Artifact type code from the catalog (user_story, business_rule, screen, ...).</summary>
    public string Type { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public ArtifactState State { get; private set; }

    /// <summary>Completeness score 0-100; null until an evaluator has scored the artifact.</summary>
    public int? Score { get; private set; }

    public ArtifactLevel Level { get; private set; }

    /// <summary>Number of the latest <see cref="ArtifactVersion"/>; 0 while no content has been written.</summary>
    public int CurrentVersion { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    public void SetScore(int? score)
    {
        if (score is int value)
        {
            Guard.InRange(value, MinScore, MaxScore);
        }

        Score = score;
        Touch();
    }

    /// <summary>Registers that a new version was written and returns its number.</summary>
    public int AdvanceVersion()
    {
        CurrentVersion++;
        Touch();
        return CurrentVersion;
    }

    public void MarkDeleted()
    {
        DeletedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
