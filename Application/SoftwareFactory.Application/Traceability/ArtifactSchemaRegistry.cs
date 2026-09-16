using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Application.Traceability;

/// <summary>
/// Catalog of artifact types and the versioned JSON Schema of each (HU-001). Every published version stays
/// available: content is validated against the current version when it is written, and interpreted with the version
/// it was written against when it is read, so evolving a schema never invalidates history.
/// </summary>
public sealed partial class ArtifactSchemaRegistry
{
    private const string ResourcePrefix = "SoftwareFactory.Application.Traceability.Schemas.";

    private readonly FrozenDictionary<string, FrozenDictionary<int, ArtifactSchema>> _schemas;

    private ArtifactSchemaRegistry(IEnumerable<ArtifactSchema> schemas)
    {
        _schemas = schemas
            .GroupBy(schema => schema.Type, StringComparer.Ordinal)
            .ToFrozenDictionary(
                group => group.Key,
                group => group.ToFrozenDictionary(schema => schema.Version),
                StringComparer.Ordinal);
    }

    /// <summary>Schemas shipped with this build, loaded once.</summary>
    public static ArtifactSchemaRegistry Embedded { get; } = new(LoadEmbedded());

    /// <summary>Builds a registry from schemas given in code; for tests that need a type with several versions.</summary>
    public static ArtifactSchemaRegistry ForTesting(IEnumerable<ArtifactSchema> schemas) => new(schemas);

    public IReadOnlyCollection<string> Types => _schemas.Keys;

    public bool Knows(string type) => _schemas.ContainsKey(type);

    public IReadOnlyList<int> VersionsOf(string type) => [.. Versions(type).Keys.Order()];

    /// <summary>Version a new write is validated against.</summary>
    public int CurrentVersionOf(string type) => Versions(type).Keys.Max();

    public ArtifactSchema Get(string type, int version) =>
        Versions(type).TryGetValue(version, out var schema) ? schema : throw new ArtifactSchemaNotFoundException(type, version);

    public ArtifactSchema GetCurrent(string type) => Get(type, CurrentVersionOf(type));

    private FrozenDictionary<int, ArtifactSchema> Versions(string type) =>
        _schemas.TryGetValue(type, out var versions) ? versions : throw new ArtifactTypeUnknownException(type);

    private static List<ArtifactSchema> LoadEmbedded()
    {
        var assembly = typeof(ArtifactSchemaRegistry).Assembly;
        List<ArtifactSchema> schemas = [];

        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            var match = ResourceName().Match(resource[ResourcePrefix.Length..]);

            if (!match.Success)
            {
                throw new InvalidOperationException($"Schema resource '{resource}' must be named <type>.v<version>.json.");
            }

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);

            schemas.Add(new ArtifactSchema(
                match.Groups["type"].Value,
                int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture),
                reader.ReadToEnd()));
        }

        return schemas.Count > 0
            ? schemas
            : throw new InvalidOperationException("No artifact schema was embedded; the build is incomplete.");
    }

    [GeneratedRegex(@"^(?<type>[a-z_]+)\.v(?<version>\d+)\.json$")]
    private static partial Regex ResourceName();
}
