namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// Filters of the artifact list (HU-001 §5) as callers express them: states and types travel by their wire names,
/// so neither the Api nor an agent needs to know the domain enums.
/// </summary>
public sealed record ArtifactFilter(Guid ProjectId)
{
    public string? Type { get; init; }

    /// <summary>draft | in_review | approved | frozen.</summary>
    public string? State { get; init; }

    /// <summary>Module the artifact belongs to, through a <c>belongs_to</c> relation.</summary>
    public Guid? ModuleId { get; init; }

    public int? MinScore { get; init; }

    public int? MaxScore { get; init; }

    public string? Title { get; init; }

    public int Skip { get; init; }

    public int Take { get; init; } = 50;
}
