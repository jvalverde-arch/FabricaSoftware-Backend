namespace SoftwareFactory.Application.Traceability.Contracts;

/// <summary>The combination source type × relation type × target type is not in the matrix (HU-002 §2).</summary>
public sealed class RelationIncompatibleException : Exception
{
    public RelationIncompatibleException(string sourceType, string relationType, string targetType, IReadOnlyList<string> allowedTargets)
        : base($"A '{sourceType}' cannot '{relationType}' a '{targetType}'.")
    {
        ArgumentNullException.ThrowIfNull(allowedTargets);

        SourceType = sourceType;
        RelationType = relationType;
        TargetType = targetType;
        AllowedTargets = allowedTargets;
    }

    public RelationIncompatibleException()
    {
    }

    public RelationIncompatibleException(string message)
        : base(message)
    {
    }

    public RelationIncompatibleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public string SourceType { get; } = string.Empty;

    public string RelationType { get; } = string.Empty;

    public string TargetType { get; } = string.Empty;

    /// <summary>What the matrix does allow for this source and this relation; empty when the pair has no rule at all.</summary>
    public IReadOnlyList<string> AllowedTargets { get; } = [];
}

/// <summary>
/// Two project-level artifacts of different projects (HU-002 §6). Crossing projects is only for what is shared: at
/// least one end has to be a tenant-global artifact, which is the boundary_contract case.
/// </summary>
public sealed class RelationCrossProjectException : Exception
{
    public RelationCrossProjectException(Guid sourceId, Guid targetId)
        : base($"Artifacts {sourceId} and {targetId} belong to different projects and neither of them is global.")
    {
        SourceId = sourceId;
        TargetId = targetId;
    }

    public RelationCrossProjectException()
    {
    }

    public RelationCrossProjectException(string message)
        : base(message)
    {
    }

    public RelationCrossProjectException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Guid SourceId { get; }

    public Guid TargetId { get; }
}

/// <summary>An artifact related to itself; the model forbids it and so does the table.</summary>
public sealed class RelationSelfReferenceException : Exception
{
    public RelationSelfReferenceException(Guid artifactId)
        : base($"Artifact {artifactId} cannot be related to itself.")
    {
        ArtifactId = artifactId;
    }

    public RelationSelfReferenceException()
    {
    }

    public RelationSelfReferenceException(string message)
        : base(message)
    {
    }

    public RelationSelfReferenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Guid ArtifactId { get; }
}

/// <summary>The same source, target and type already exist; the unique index of the table says the same.</summary>
public sealed class RelationAlreadyExistsException : Exception
{
    public RelationAlreadyExistsException(Guid sourceId, Guid targetId, string type)
        : base($"A '{type}' relation from {sourceId} to {targetId} already exists.")
    {
        SourceId = sourceId;
        TargetId = targetId;
        RelationType = type;
    }

    public RelationAlreadyExistsException()
    {
    }

    public RelationAlreadyExistsException(string message)
        : base(message)
    {
    }

    public RelationAlreadyExistsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Guid SourceId { get; }

    public Guid TargetId { get; }

    public string RelationType { get; } = string.Empty;
}

public sealed class RelationNotFoundException : Exception
{
    public RelationNotFoundException(Guid relationId)
        : base($"Relation {relationId} does not exist for this tenant.")
    {
        RelationId = relationId;
    }

    public RelationNotFoundException()
    {
    }

    public RelationNotFoundException(string message)
        : base(message)
    {
    }

    public RelationNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Guid RelationId { get; }
}

/// <summary>More (or fewer) hops than the neighborhood answers (HU-002 §3).</summary>
public sealed class RelationLevelsOutOfRangeException : Exception
{
    public RelationLevelsOutOfRangeException(int levels, int maxLevels)
        : base($"The neighborhood goes from 1 to {maxLevels} levels; {levels} was asked for.")
    {
        Levels = levels;
        MaxLevels = maxLevels;
    }

    public RelationLevelsOutOfRangeException()
    {
    }

    public RelationLevelsOutOfRangeException(string message)
        : base(message)
    {
    }

    public RelationLevelsOutOfRangeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public int Levels { get; }

    public int MaxLevels { get; }
}

/// <summary>A relation type that is not in the catalog.</summary>
public sealed class RelationTypeUnknownException : Exception
{
    public RelationTypeUnknownException(string relationType)
        : base($"'{relationType}' is not a relation type of the catalog.")
    {
        RelationType = relationType;
    }

    public RelationTypeUnknownException()
    {
    }

    public RelationTypeUnknownException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public string RelationType { get; } = string.Empty;
}
