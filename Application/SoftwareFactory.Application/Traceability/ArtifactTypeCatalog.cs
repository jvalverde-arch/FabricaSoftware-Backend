namespace SoftwareFactory.Application.Traceability;

/// <summary>
/// The R1 artifact catalog in the order the sprint spec lists it. The order is not decoration: it is the second key
/// of the deterministic ordering of the tree's children (HU-004, hierarchy rule 2), so it travels to the queries as
/// a parameter from here and from nowhere else.
/// </summary>
/// <remarks>
/// <see cref="ArtifactSchemaRegistry"/> knows which types exist — it loads one schema per type — but a dictionary of
/// schemas has no order to give. A test keeps both in step: every type of the registry is in this list and nothing
/// else is, so adding a type to the catalog without placing it here fails the build's test run, not production.
/// </remarks>
public static class ArtifactTypeCatalog
{
    /// <summary>Artifact types in catalog order (sprint-01, R1 catalog).</summary>
    public static IReadOnlyList<string> InOrder { get; } =
    [
        "module",
        "user_story",
        "functional_requirement",
        "non_functional_requirement",
        "business_rule",
        "use_case",
        "screen",
        "actor",
        "api",
        "data_entity",
        "architecture_component",
        "adr",
        "compliance_requirement",
        "test_case",
        "boundary_contract",
    ];

    /// <summary>The type the tree hangs at its first level (hierarchy rule 1).</summary>
    public const string Module = "module";
}
