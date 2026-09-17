using Microsoft.Extensions.Logging;

namespace SoftwareFactory.Application.Traceability;

/// <summary>Structured events of the traceability module; never the content, which is the tenant's material.</summary>
internal static partial class TraceabilityLog
{
    [LoggerMessage(EventId = 5000, Level = LogLevel.Information, Message = "Artifact {ArtifactId} of type {ArtifactType} created in project {ProjectId} (schema v{SchemaVersion}).")]
    public static partial void Created(this ILogger logger, Guid artifactId, string artifactType, Guid projectId, int schemaVersion);

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Artifact {ArtifactId} updated to version {Version} (schema v{SchemaVersion}).")]
    public static partial void Updated(this ILogger logger, Guid artifactId, int version, int schemaVersion);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Information, Message = "Artifact {ArtifactId} moved from {From} to {To}.")]
    public static partial void StateChanged(this ILogger logger, Guid artifactId, Domain.Traceability.ArtifactState from, Domain.Traceability.ArtifactState to);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Information, Message = "Artifact {ArtifactId} deleted logically.")]
    public static partial void Deleted(this ILogger logger, Guid artifactId);

    [LoggerMessage(EventId = 5005, Level = LogLevel.Information, Message = "Artifact {ArtifactId} moved from schema v{FromSchemaVersion} to v{ToSchemaVersion}; its score was invalidated because it was measured against the previous schema.")]
    public static partial void ScoreInvalidated(this ILogger logger, Guid artifactId, int fromSchemaVersion, int toSchemaVersion);

    [LoggerMessage(EventId = 5004, Level = LogLevel.Warning, Message = "Artifact {ArtifactId} could not be deleted: {RelationCount} relation(s) still point at it.")]
    public static partial void DeleteBlocked(this ILogger logger, Guid artifactId, int relationCount);

    [LoggerMessage(EventId = 5010, Level = LogLevel.Information, Message = "Relation {RelationId} created: {SourceId} {RelationType} {TargetId}.")]
    public static partial void RelationCreated(this ILogger logger, Guid relationId, Guid sourceId, Guid targetId, string relationType);

    [LoggerMessage(EventId = 5011, Level = LogLevel.Information, Message = "Relation {RelationId} of type {RelationType} deleted.")]
    public static partial void RelationDeleted(this ILogger logger, Guid relationId, string relationType);

    [LoggerMessage(EventId = 5012, Level = LogLevel.Warning, Message = "Relation refused by the compatibility matrix: a '{SourceType}' cannot '{RelationType}' a '{TargetType}'.")]
    public static partial void RelationRefused(this ILogger logger, string sourceType, string relationType, string targetType);
}
