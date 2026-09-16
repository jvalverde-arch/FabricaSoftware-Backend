namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>
/// The content does not satisfy the schema of its type. Carries one entry per offending field so the Api answers
/// 422 with detail per field (HU-001 contracts).
/// </summary>
public sealed class ArtifactValidationException : Exception
{
    public ArtifactValidationException(string artifactType, IReadOnlyList<SchemaValidationError> errors)
        : base($"The content does not satisfy the schema of '{artifactType}'.")
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArtifactType = artifactType;
        Errors = errors;
    }

    public ArtifactValidationException()
    {
    }

    public ArtifactValidationException(string message)
        : base(message)
    {
    }

    public ArtifactValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public string ArtifactType { get; } = string.Empty;

    public IReadOnlyList<SchemaValidationError> Errors { get; } = [];
}
