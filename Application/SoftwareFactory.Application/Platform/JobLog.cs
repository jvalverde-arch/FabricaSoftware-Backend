using Microsoft.Extensions.Logging;

namespace SoftwareFactory.Application.Platform;

/// <summary>Structured events of the job worker (estandar-backend.md §3): never the payload, which may carry data of the tenant.</summary>
internal static partial class JobLog
{
    [LoggerMessage(EventId = 4000, Level = LogLevel.Information, Message = "Job {JobId} of type {JobType} finished (attempt {Attempt}) in {ElapsedMs} ms.")]
    public static partial void Succeeded(this ILogger logger, Guid jobId, string jobType, int attempt, double elapsedMs);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Warning, Message = "Job {JobId} of type {JobType} failed on attempt {Attempt}; it will run again at {AvailableAt}.")]
    public static partial void Retrying(this ILogger logger, Exception exception, Guid jobId, string jobType, int attempt, DateTimeOffset availableAt);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Error, Message = "Job {JobId} of type {JobType} gave up after {Attempt} attempts.")]
    public static partial void GaveUp(this ILogger logger, Exception? exception, Guid jobId, string jobType, int attempt);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Error, Message = "Job {JobId} declares type {JobType}, which no handler serves.")]
    public static partial void UnknownType(this ILogger logger, Guid jobId, string jobType);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Warning, Message = "Job {JobId} was claimed but is no longer in the database; nothing to run.")]
    public static partial void Vanished(this ILogger logger, Guid jobId);

    [LoggerMessage(EventId = 4005, Level = LogLevel.Information, Message = "Job {JobId} was interrupted; its lease is released so another worker takes it.")]
    public static partial void Interrupted(this ILogger logger, Guid jobId);

    [LoggerMessage(EventId = 4006, Level = LogLevel.Information, Message = "Job {JobId} is at {Phase} ({ProgressPercent}%).")]
    public static partial void Progress(this ILogger logger, Guid jobId, string phase, int? progressPercent);
}
