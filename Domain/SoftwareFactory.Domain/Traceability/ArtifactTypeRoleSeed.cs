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
    /// <remarks>
    /// <para>
    /// <c>admin</c> and <c>reader</c> are absent on purpose, and adding them would be a change of governance, not a
    /// fix. The platform administrator runs tenants, users and connections — not the functional content — so they are
    /// competent over no artifact, and everything they write is born as a note pending whoever does answer for it.
    /// A reader reads. Putting either of them in the map would quietly give one account the power to settle any note
    /// in the tenant, which is the opposite of what the decision log is for (doc 01, principles 6-7).
    /// </para>
    /// </remarks>
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
