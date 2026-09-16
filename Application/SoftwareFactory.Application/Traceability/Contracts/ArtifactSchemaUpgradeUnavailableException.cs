using System.Globalization;

namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// An artifact written against an older schema cannot be edited because the step to the next version has no
/// published upgrader. Reading it still works: only the write is refused, and it says exactly which step is missing.
/// </summary>
public sealed class ArtifactSchemaUpgradeUnavailableException : Exception
{
    public ArtifactSchemaUpgradeUnavailableException(string artifactType, int fromVersion, int toVersion)
        : base(string.Create(
            CultureInfo.InvariantCulture,
            $"Artifact type '{artifactType}' has no upgrader from schema v{fromVersion} to v{toVersion}; the content cannot be edited until one is published."))
    {
        ArtifactType = artifactType;
        FromVersion = fromVersion;
        ToVersion = toVersion;
    }

    public ArtifactSchemaUpgradeUnavailableException()
    {
    }

    public ArtifactSchemaUpgradeUnavailableException(string message)
        : base(message)
    {
    }

    public ArtifactSchemaUpgradeUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public string ArtifactType { get; } = string.Empty;

    public int FromVersion { get; }

    public int ToVersion { get; }
}
