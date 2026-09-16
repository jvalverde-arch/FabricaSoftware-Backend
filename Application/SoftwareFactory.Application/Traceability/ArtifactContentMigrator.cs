using System.Collections.Frozen;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Application.Traceability;

/// <summary>
/// Brings stored content up to the current schema of its type, one published upgrader at a time (HU-001).
/// It runs on edit, never in bulk: versions already written are immutable history, and rewriting them would forge
/// the author and the date of a change nobody made.
/// </summary>
public sealed class ArtifactContentMigrator
{
    private readonly ArtifactSchemaRegistry _registry;
    private readonly FrozenDictionary<(string Type, int From), IArtifactContentUpgrader> _upgraders;

    public ArtifactContentMigrator(ArtifactSchemaRegistry registry, IEnumerable<IArtifactContentUpgrader> upgraders)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(upgraders);

        _registry = registry;

        var byStep = new Dictionary<(string, int), IArtifactContentUpgrader>();

        foreach (var upgrader in upgraders)
        {
            var step = (upgrader.ArtifactType, upgrader.FromVersion);

            if (!byStep.TryAdd(step, upgrader))
            {
                throw new InvalidOperationException(
                    $"Two upgraders claim the step v{upgrader.FromVersion} → v{upgrader.FromVersion + 1} of '{upgrader.ArtifactType}'; only one may.");
            }
        }

        _upgraders = byStep.ToFrozenDictionary();
    }

    /// <summary>
    /// Walks <paramref name="content"/> from <paramref name="fromVersion"/> to the current schema of the type.
    /// </summary>
    /// <exception cref="ArtifactSchemaNotFoundException">The stored version is unknown to this deployment.</exception>
    /// <exception cref="ArtifactSchemaUpgradeUnavailableException">A step of the way has no published upgrader.</exception>
    public UpgradedContent UpgradeToCurrent(string type, string content, int fromVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        // Fails when the data was written by a newer deployment than this one: never silently downgrade.
        _ = _registry.Get(type, fromVersion);
        var current = _registry.CurrentVersionOf(type);

        if (fromVersion == current)
        {
            return new UpgradedContent(content, current, Changed: false);
        }

        var upgraded = content;

        for (var version = fromVersion; version < current; version++)
        {
            if (!_upgraders.TryGetValue((type, version), out var upgrader))
            {
                throw new ArtifactSchemaUpgradeUnavailableException(type, version, version + 1);
            }

            upgraded = upgrader.Upgrade(upgraded);
        }

        return new UpgradedContent(upgraded, current, Changed: true);
    }
}
