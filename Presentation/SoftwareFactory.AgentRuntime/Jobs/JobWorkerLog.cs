namespace SoftwareFactory.AgentRuntime.Jobs;

internal static partial class JobWorkerLog
{
    [LoggerMessage(EventId = 4200, Level = LogLevel.Information, Message = "Job worker started: polling every {PollInterval}, lease {Lease}.")]
    public static partial void WorkerStarted(this ILogger logger, TimeSpan pollInterval, TimeSpan lease);

    [LoggerMessage(EventId = 4201, Level = LogLevel.Information, Message = "Job worker stopped.")]
    public static partial void WorkerStopped(this ILogger logger);

    [LoggerMessage(EventId = 4202, Level = LogLevel.Warning, Message = "Job worker is disabled by configuration (Jobs:Enabled).")]
    public static partial void WorkerDisabled(this ILogger logger);

    [LoggerMessage(EventId = 4203, Level = LogLevel.Error, Message = "A round of the job worker failed; the loop continues.")]
    public static partial void RoundFailed(this ILogger logger, Exception exception);
}
