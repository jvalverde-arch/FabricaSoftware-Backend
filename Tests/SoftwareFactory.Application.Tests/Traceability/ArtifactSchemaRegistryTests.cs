using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Application.Tests.Traceability;

/// <summary>
/// The schema registry of HU-001: one JSON Schema per artifact type and version, shipped with the application.
/// Writing validates against the current version; reading an old version must still find the schema it was written
/// against, which is what keeps history readable when a type evolves.
/// </summary>
public sealed class ArtifactSchemaRegistryTests
{
    private static readonly ArtifactSchemaRegistry _registry = ArtifactSchemaRegistry.Embedded;

    [Fact]
    public void The_catalog_of_R1_is_complete()
    {
        var expected = new[]
        {
            "module", "user_story", "functional_requirement", "non_functional_requirement", "business_rule",
            "use_case", "screen", "actor", "api", "data_entity", "architecture_component", "adr",
            "compliance_requirement", "test_case", "boundary_contract",
        };

        Assert.Equal(expected.Order(), _registry.Types.Order());
    }

    [Theory]
    [InlineData("user_story")]
    [InlineData("boundary_contract")]
    [InlineData("test_case")]
    public void Every_type_has_a_current_version_and_its_schema(string type)
    {
        var current = _registry.CurrentVersionOf(type);

        Assert.True(current >= 1);
        var schema = _registry.Get(type, current);
        Assert.Equal(type, schema.Type);
        Assert.Equal(current, schema.Version);
        Assert.Contains("\"type\": \"object\"", schema.Json, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_type_is_rejected_by_name()
    {
        var exception = Assert.Throws<ArtifactTypeUnknownException>(() => _registry.CurrentVersionOf("dragon"));

        Assert.Equal("dragon", exception.ArtifactType);
        Assert.False(_registry.Knows("dragon"));
        Assert.True(_registry.Knows("user_story"));
    }

    [Fact]
    public void Asking_for_a_version_that_was_never_published_is_an_error() =>
        Assert.Throws<ArtifactSchemaNotFoundException>(() => _registry.Get("user_story", 99));

    [Fact]
    public void The_schema_of_an_old_version_stays_available_after_the_type_evolves()
    {
        // A type with two published versions: both must be reachable, because stored content is interpreted with
        // the schema it was written against and never re-validated against the newer one.
        var registry = ArtifactSchemaRegistry.ForTesting(
        [
            new ArtifactSchema("user_story", 1, """{"type":"object","required":["as_a"]}"""),
            new ArtifactSchema("user_story", 2, """{"type":"object","required":["as_a","so_that"]}"""),
        ]);

        Assert.Equal(2, registry.CurrentVersionOf("user_story"));
        Assert.Equal([1, 2], registry.VersionsOf("user_story"));
        Assert.Contains("so_that", registry.Get("user_story", 2).Json, StringComparison.Ordinal);
        Assert.DoesNotContain("so_that", registry.Get("user_story", 1).Json, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_published_schema_is_valid_json_with_an_identifier()
    {
        foreach (var type in _registry.Types)
        {
            foreach (var version in _registry.VersionsOf(type))
            {
                var schema = _registry.Get(type, version);
                using var document = System.Text.Json.JsonDocument.Parse(schema.Json);
                Assert.True(document.RootElement.TryGetProperty("$id", out var id), $"{type} v{version} needs $id");
                Assert.Contains($"/{type}/v{version}", id.GetString() ?? string.Empty, StringComparison.Ordinal);
            }
        }
    }
}
