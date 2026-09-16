using System.Globalization;

namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// The requested schema version was never published for that type. It usually means stored content points at a
/// version that the deployed application does not carry — the deployment is older than the data.
/// </summary>
public sealed class ArtifactSchemaNotFoundException : Exception
{
    public ArtifactSchemaNotFoundException(string type, int version)
        : base(string.Create(CultureInfo.InvariantCulture, $"No schema v{version} is published for artifact type '{type}'."))
    {
        ArtifactType = type;
        Version = version;
    }

    public ArtifactSchemaNotFoundException()
    {
    }

    public ArtifactSchemaNotFoundException(string message)
        : base(message)
    {
    }

    public ArtifactSchemaNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public string ArtifactType { get; } = string.Empty;

    public int Version { get; }
}
