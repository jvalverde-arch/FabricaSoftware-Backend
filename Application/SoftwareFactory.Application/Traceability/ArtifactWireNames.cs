using SoftwareFactory.Application.Traceability.Contracts;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Application.Traceability;

/// <summary>
/// Translation between the domain enums and the wire names of the module contract. It lives inside the module: the
/// callers (Api, agents) only ever see the strings.
/// </summary>
internal static class ArtifactWireNames
{
    public static string Of(ArtifactState state) => state switch
    {
        ArtifactState.Draft => ArtifactNames.Draft,
        ArtifactState.InReview => ArtifactNames.InReview,
        ArtifactState.Approved => ArtifactNames.Approved,
        ArtifactState.Frozen => ArtifactNames.Frozen,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    public static string Of(ArtifactLevel level) => level == ArtifactLevel.Global ? ArtifactNames.GlobalLevel : ArtifactNames.ProjectLevel;

    public static string Of(AuthorType type) => type == AuthorType.Agent ? ArtifactNames.Agent : ArtifactNames.Human;

    public static ArtifactState? StateOrNull(string? state) => state switch
    {
        null or "" => null,
        ArtifactNames.Draft => ArtifactState.Draft,
        ArtifactNames.InReview => ArtifactState.InReview,
        ArtifactNames.Approved => ArtifactState.Approved,
        ArtifactNames.Frozen => ArtifactState.Frozen,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown artifact state."),
    };

    public static ArtifactLevel Level(string? level) => level switch
    {
        null or "" or ArtifactNames.ProjectLevel => ArtifactLevel.Project,
        ArtifactNames.GlobalLevel => ArtifactLevel.Global,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown artifact level."),
    };
}
