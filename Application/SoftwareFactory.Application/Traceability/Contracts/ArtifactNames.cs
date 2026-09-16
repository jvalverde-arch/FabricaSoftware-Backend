namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>Wire names of the module: the same snake_case values the database stores.</summary>
public static class ArtifactNames
{
    public const string ProjectLevel = "project";
    public const string GlobalLevel = "global";

    public const string Draft = "draft";
    public const string InReview = "in_review";
    public const string Approved = "approved";
    public const string Frozen = "frozen";

    public const string Human = "human";
    public const string Agent = "agent";
}
