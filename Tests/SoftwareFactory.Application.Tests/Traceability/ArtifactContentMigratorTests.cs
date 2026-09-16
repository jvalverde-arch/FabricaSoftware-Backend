using System.Text.Json;
using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Application.Tests.Traceability;

/// <summary>
/// What happens to content already stored when the schema of its type changes (HU-001). The answer: nothing, until
/// somebody edits it. Reading keeps using the schema the content was written against; an edit upgrades the content
/// through the published upgraders and writes a new version stamped with the current schema. History is never
/// rewritten: doing so would forge authorship and dates.
/// </summary>
public sealed class ArtifactContentMigratorTests
{
    private static readonly ArtifactSchemaRegistry _registry = ArtifactSchemaRegistry.ForTesting(
    [
        new ArtifactSchema("user_story", 1, """{"type":"object"}"""),
        new ArtifactSchema("user_story", 2, """{"type":"object"}"""),
        new ArtifactSchema("user_story", 3, """{"type":"object"}"""),
        new ArtifactSchema("screen", 1, """{"type":"object"}"""),
    ]);

    [Fact]
    public void Content_already_on_the_current_version_is_left_alone()
    {
        var migrator = new ArtifactContentMigrator(_registry, [new AddSoThat(), new SplitCriteria()]);

        var result = migrator.UpgradeToCurrent("user_story", """{"as_a":"usuaria"}""", fromVersion: 3);

        Assert.Equal(3, result.Version);
        Assert.False(result.Changed);
        Assert.Equal("""{"as_a":"usuaria"}""", result.Content);
    }

    [Fact]
    public void An_edit_walks_the_content_through_every_published_upgrader()
    {
        var migrator = new ArtifactContentMigrator(_registry, [new AddSoThat(), new SplitCriteria()]);

        var result = migrator.UpgradeToCurrent("user_story", """{"as_a":"usuaria","criteria":"a; b"}""", fromVersion: 1);

        Assert.Equal(3, result.Version);
        Assert.True(result.Changed);

        using var upgraded = JsonDocument.Parse(result.Content);
        Assert.Equal("por definir", upgraded.RootElement.GetProperty("so_that").GetString());
        Assert.Equal(["a", "b"], upgraded.RootElement.GetProperty("acceptance_criteria").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void A_breaking_step_without_upgrader_refuses_the_write_and_says_which_step_is_missing()
    {
        // Only the first step is published: 2 → 3 has no upgrader.
        var migrator = new ArtifactContentMigrator(_registry, [new AddSoThat()]);

        var exception = Assert.Throws<ArtifactSchemaUpgradeUnavailableException>(
            () => migrator.UpgradeToCurrent("user_story", """{"as_a":"usuaria"}""", fromVersion: 1));

        Assert.Equal("user_story", exception.ArtifactType);
        Assert.Equal(2, exception.FromVersion);
        Assert.Equal(3, exception.ToVersion);
    }

    [Fact]
    public void Content_written_by_a_newer_deployment_is_not_silently_downgraded()
    {
        var migrator = new ArtifactContentMigrator(_registry, []);

        Assert.Throws<ArtifactSchemaNotFoundException>(() => migrator.UpgradeToCurrent("user_story", "{}", fromVersion: 7));
    }

    [Fact]
    public void A_type_with_a_single_version_needs_no_upgraders_at_all()
    {
        var migrator = new ArtifactContentMigrator(_registry, []);

        var result = migrator.UpgradeToCurrent("screen", """{"purpose":"listar"}""", fromVersion: 1);

        Assert.False(result.Changed);
        Assert.Equal(1, result.Version);
    }

    [Fact]
    public void Two_upgraders_for_the_same_step_are_a_configuration_error_found_at_startup()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => new ArtifactContentMigrator(_registry, [new AddSoThat(), new AddSoThat()]));

        Assert.Contains("user_story", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>v1 → v2: the story gains a mandatory «so_that»; existing content gets a placeholder to be completed.</summary>
    private sealed class AddSoThat : IArtifactContentUpgrader
    {
        public string ArtifactType => "user_story";

        public int FromVersion => 1;

        public string Upgrade(string content)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(content)!.AsObject();
            node["so_that"] = "por definir";
            return node.ToJsonString();
        }
    }

    /// <summary>v2 → v3: the free-text criteria become a list.</summary>
    private sealed class SplitCriteria : IArtifactContentUpgrader
    {
        public string ArtifactType => "user_story";

        public int FromVersion => 2;

        public string Upgrade(string content)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(content)!.AsObject();
            var criteria = node["criteria"]?.GetValue<string>() ?? string.Empty;
            node.Remove("criteria");
            node["acceptance_criteria"] = new System.Text.Json.Nodes.JsonArray(
                [.. criteria.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(item => System.Text.Json.Nodes.JsonValue.Create(item))]);
            return node.ToJsonString();
        }
    }
}
