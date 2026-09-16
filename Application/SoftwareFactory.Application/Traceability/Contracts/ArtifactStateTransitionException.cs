namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// The life cycle does not allow that move (HU-001 §1). It is a client mistake, not a server failure: the Api
/// answers 409 saying which transition was attempted.
/// </summary>
public sealed class ArtifactStateTransitionException : Exception
{
    public ArtifactStateTransitionException(Guid artifactId, string from, string to)
        : base($"Artifact {artifactId} cannot move from {from} to {to}.")
    {
        ArtifactId = artifactId;
        From = from;
        To = to;
    }

    public ArtifactStateTransitionException()
    {
    }

    public ArtifactStateTransitionException(string message)
        : base(message)
    {
    }

    public ArtifactStateTransitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Guid ArtifactId { get; }

    public string From { get; } = string.Empty;

    public string To { get; } = string.Empty;
}
