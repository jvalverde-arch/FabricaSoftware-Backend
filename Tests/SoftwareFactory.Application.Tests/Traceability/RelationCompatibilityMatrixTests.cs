using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Application.Tests.Traceability;

/// <summary>The shipped matrix (HU-002 §1). It is data, so these tests read like the table they check.</summary>
public sealed class RelationCompatibilityMatrixTests
{
    private static readonly RelationCompatibilityMatrix _matrix = RelationCompatibilityMatrix.Embedded;

    [Theory]
    [InlineData("test_case", RelationNames.Validates, "user_story")]
    [InlineData("screen", RelationNames.BelongsTo, "module")]
    [InlineData("module", RelationNames.Exposes, "boundary_contract")]
    [InlineData("module", RelationNames.Consumes, "boundary_contract")]
    [InlineData("user_story", RelationNames.Implements, "functional_requirement")]
    [InlineData("adr", RelationNames.Affects, "architecture_component")]
    [InlineData("module", RelationNames.DependsOn, "module")]
    [InlineData("functional_requirement", RelationNames.DerivesFrom, "use_case")]
    public void The_catalog_allows_the_combinations_of_the_spec(string source, string relation, string target) =>
        Assert.True(_matrix.Allows(source, relation, target), $"The matrix should allow '{source}' {relation} '{target}'.");

    [Theory]
    [InlineData("user_story", RelationNames.Validates, "test_case")]   // backwards: the test validates the story
    [InlineData("module", RelationNames.BelongsTo, "user_story")]
    [InlineData("boundary_contract", RelationNames.Exposes, "module")] // backwards: the module exposes the contract
    [InlineData("screen", RelationNames.Consumes, "boundary_contract")]
    [InlineData("actor", RelationNames.Implements, "user_story")]
    public void The_catalog_refuses_what_the_model_does_not_mean(string source, string relation, string target) =>
        Assert.False(_matrix.Allows(source, relation, target), $"The matrix should refuse '{source}' {relation} '{target}'.");

    [Fact]
    public void The_eight_relation_types_of_R1_are_in_the_catalog() =>
        Assert.Equal(
            [
                RelationNames.Affects, RelationNames.BelongsTo, RelationNames.Consumes, RelationNames.DependsOn,
                RelationNames.DerivesFrom, RelationNames.Exposes, RelationNames.Implements, RelationNames.Validates,
            ],
            _matrix.RelationTypes.Order(StringComparer.Ordinal));

    [Fact]
    public void A_relation_type_outside_the_catalog_is_reported_as_such() =>
        Assert.Throws<RelationTypeUnknownException>(() => _matrix.AllowedTargets("module", "inspires"));

    [Fact]
    public void A_refused_pair_can_say_what_it_would_have_allowed() =>
        Assert.Equal(["boundary_contract"], _matrix.AllowedTargets("module", RelationNames.Exposes));

    [Fact]
    public void A_source_with_no_rule_for_that_relation_allows_nothing() =>
        Assert.Empty(_matrix.AllowedTargets("actor", RelationNames.Exposes));
}
