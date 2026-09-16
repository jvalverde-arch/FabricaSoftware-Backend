namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>The artifact type is not in the catalog. The catalog is closed and extended by configuration, not by callers.</summary>
public sealed class ArtifactTypeUnknownException : Exception
{
    public ArtifactTypeUnknownException(string type)
        : base($"'{type}' is not an artifact type of the catalog.")
    {
        ArtifactType = type;
    }

    public ArtifactTypeUnknownException()
    {
    }

    public ArtifactTypeUnknownException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public string ArtifactType { get; } = string.Empty;
}
