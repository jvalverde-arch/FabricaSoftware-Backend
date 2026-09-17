using System.Collections.Frozen;
using System.Text.Json;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Application.Traceability;

/// <summary>
/// Which relation types may join which artifact types (HU-002 §1). The matrix is data, not code: it ships as an
/// embedded catalog so that adding a combination is editing a table, and the rule that refuses one can say exactly
/// what it allowed instead.
/// </summary>
public sealed class RelationCompatibilityMatrix
{
    private const string ResourceName = "SoftwareFactory.Application.Traceability.Relations.relation_compatibility.v1.json";

    // Declared before Embedded on purpose: static initializers run in textual order, and reading the catalog with a
    // null options object silently matches nothing.
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Allowed targets by relation type and source type.</summary>
    private readonly FrozenDictionary<string, FrozenDictionary<string, FrozenSet<string>>> _rules;

    private RelationCompatibilityMatrix(IEnumerable<CompatibilityRule> rules)
    {
        _rules = rules
            .SelectMany(rule => rule.Sources.Select(source => (rule.Relation, Source: source, rule.Targets)))
            .GroupBy(entry => entry.Relation, StringComparer.Ordinal)
            .ToFrozenDictionary(
                byRelation => byRelation.Key,
                byRelation => byRelation
                    .GroupBy(entry => entry.Source, StringComparer.Ordinal)
                    .ToFrozenDictionary(
                        bySource => bySource.Key,
                        bySource => bySource.SelectMany(entry => entry.Targets).ToFrozenSet(StringComparer.Ordinal),
                        StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    /// <summary>Matrix shipped with this build, read once.</summary>
    public static RelationCompatibilityMatrix Embedded { get; } = new(LoadEmbedded());

    /// <summary>Builds a matrix from rules given in code; for tests that need a combination of their own.</summary>
    public static RelationCompatibilityMatrix ForTesting(IEnumerable<CompatibilityRule> rules) => new(rules);

    /// <summary>Relation types of the catalog.</summary>
    public IReadOnlyCollection<string> RelationTypes => _rules.Keys;

    public bool Knows(string relationType) => _rules.ContainsKey(relationType);

    public bool Allows(string sourceType, string relationType, string targetType) =>
        AllowedTargets(sourceType, relationType).Contains(targetType);

    /// <summary>What this source type may point at with this relation; empty when the pair has no rule.</summary>
    public IReadOnlyList<string> AllowedTargets(string sourceType, string relationType)
    {
        if (!_rules.TryGetValue(relationType, out var bySource))
        {
            throw new RelationTypeUnknownException(relationType);
        }

        return bySource.TryGetValue(sourceType, out var targets) ? [.. targets.Order(StringComparer.Ordinal)] : [];
    }

    private static List<CompatibilityRule> LoadEmbedded()
    {
        var assembly = typeof(RelationCompatibilityMatrix).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded relation matrix '{ResourceName}' is missing from the build.");

        var catalog = JsonSerializer.Deserialize<CompatibilityCatalog>(stream, _jsonOptions)
            ?? throw new InvalidOperationException($"The embedded relation matrix '{ResourceName}' is empty.");

        return catalog.Rules;
    }

    private sealed record CompatibilityCatalog(int Version, List<CompatibilityRule> Rules);
}

/// <summary>One row of the matrix: a relation type and the source and target types it joins.</summary>
public sealed record CompatibilityRule(string Relation, IReadOnlyList<string> Sources, IReadOnlyList<string> Targets);
