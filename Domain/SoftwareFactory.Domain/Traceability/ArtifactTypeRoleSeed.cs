using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Traceability;

/// <summary>
/// Base competence map of R1 (HU-003 §3), the seed every tenant starts from. Frozen: the migration that seeds it
/// iterates this list, so changing it here does not rewrite what a live tenant has already been given — that is an
/// edit of the tenant's configuration, not of the seed.
/// </summary>
public static class ArtifactTypeRoleSeed
{
    /// <summary>
    /// Types and the role competent for each. <c>non_functional_requirement</c> is the only type with two: the
    /// business states it and the architecture is what has to hold it up.
    /// </summary>
    public static IReadOnlyList<(string ArtifactType, Role Role)> Base { get; } =
    [
        ("module", Role.Functional),
        ("user_story", Role.Functional),
        ("functional_requirement", Role.Functional),
        ("business_rule", Role.Functional),
        ("use_case", Role.Functional),
        ("screen", Role.Functional),
        ("actor", Role.Functional),
        ("architecture_component", Role.Architect),
        ("adr", Role.Architect),
        ("boundary_contract", Role.Architect),
        ("api", Role.Architect),
        ("data_entity", Role.Architect),
        ("test_case", Role.Qa),
        ("compliance_requirement", Role.Compliance),
        ("non_functional_requirement", Role.Functional),
        ("non_functional_requirement", Role.Architect),
    ];
}
