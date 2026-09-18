using Microsoft.Extensions.Logging;

namespace SoftwareFactory.Application.Project;

/// <summary>Structured events of the project module.</summary>
internal static partial class ProjectLog
{
    [LoggerMessage(EventId = 5100, Level = LogLevel.Information, Message = "Project {ProjectId} created with name {ProjectName}.")]
    public static partial void ProjectCreated(this ILogger logger, Guid projectId, string projectName);

    [LoggerMessage(EventId = 5101, Level = LogLevel.Information, Message = "A second write claimed the project name {ProjectName} at the same time; the unique index settled it.")]
    public static partial void ProjectNameRaceLost(this ILogger logger, string projectName);
}
