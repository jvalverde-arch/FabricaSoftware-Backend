namespace SoftwareFactory.Api.Jobs;

internal static partial class JobStreamLog
{
    [LoggerMessage(EventId = 4300, Level = LogLevel.Debug, Message = "Progress stream of job {JobId} closed: the client went away.")]
    public static partial void StreamClosedByClient(this ILogger logger, Guid jobId);

    [LoggerMessage(EventId = 4301, Level = LogLevel.Information, Message = "Progress stream of job {JobId} closed after {MaxDuration}; the client may reconnect.")]
    public static partial void StreamTimedOut(this ILogger logger, Guid jobId, TimeSpan maxDuration);
}
