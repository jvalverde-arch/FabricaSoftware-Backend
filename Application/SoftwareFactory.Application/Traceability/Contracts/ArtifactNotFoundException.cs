namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>No artifact with that id in the current tenant. Another tenant's artifact simply does not exist here.</summary>
public sealed class ArtifactNotFoundException : Exception
{
    public ArtifactNotFoundException(Guid artifactId)
        : base($"Artifact {artifactId} does not exist.")
    {
        ArtifactId = artifactId;
    }

    public ArtifactNotFoundException()
    {
    }

    public ArtifactNotFoundException(string message)
        : base(message)
    {
    }

    public ArtifactNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Guid ArtifactId { get; }
}
