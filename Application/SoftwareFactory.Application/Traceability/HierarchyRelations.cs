using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Application.Traceability;

/// <summary>
/// The relation types that draw the tree, in order of precedence (HU-004, hierarchy rule 2). This is the only place
/// the set exists: the queries receive it as a parameter instead of naming the types, so reordering this list
/// reorders the SQL too and the two can never drift apart.
/// </summary>
/// <remarks>
/// Precedence does not pick a parent — an artifact with two parents hangs under both (rule 4). It does two things:
/// it settles which edge type a pair is joined by when more than one hierarchical relation links them, and it orders
/// the children of a node, which is what reproduces the walk of criterion 1 (a story's requirements before its tests).
/// </remarks>
public static class HierarchyRelations
{
    /// <summary>Hierarchical relation types, highest precedence first.</summary>
    public static IReadOnlyList<string> ByPrecedence { get; } =
    [
        RelationNames.BelongsTo,
        RelationNames.Implements,
        RelationNames.Validates,
    ];

    public static bool Contains(string relationType) => ByPrecedence.Contains(relationType, StringComparer.Ordinal);
}
