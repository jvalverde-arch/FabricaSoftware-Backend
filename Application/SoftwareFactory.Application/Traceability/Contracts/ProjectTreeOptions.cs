namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// Limits of the project tree (HU-004). They are configuration and not numbers buried in a query: the depth a
/// hierarchy is allowed to reach and how wide a filter may be before it stops being a filter depend on the tenant's
/// material, and both have to move without touching code.
/// </summary>
public sealed class ProjectTreeOptions
{
    public const string SectionName = "ProjectTree";

    /// <summary>Ceiling no configuration may cross, so a bad value cannot turn a walk into a scan of the project.</summary>
    public const int DepthCeiling = 20;

    /// <summary>Ceiling on the filtered mode, for the same reason.</summary>
    public const int MatchCeiling = 5_000;

    /// <summary>How deep the hierarchy may go before a branch is cut (hierarchy rule 5).</summary>
    public int MaxDepth { get; set; } = 10;

    /// <summary>
    /// Matches the filtered mode returns at most (HU-004, filtered mode). The cut is on the matches and never on the
    /// nodes: what comes back is complete for the matches it carries, so a partial tree does not lie about its shape.
    /// </summary>
    public int MaxMatches { get; set; } = 500;

    public bool IsValid() =>
        MaxDepth is > 0 and <= DepthCeiling && MaxMatches is > 0 and <= MatchCeiling;
}
