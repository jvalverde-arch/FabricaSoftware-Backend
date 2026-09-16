using Microsoft.Extensions.Logging;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Infrastructure.Jobs;

/// <summary>Structured events of the queue itself; the payload is never logged (it carries tenant data).</summary>
internal static partial class JobInfrastructureLog
{
    [LoggerMessage(EventId = 4100, Level = LogLevel.Information, Message = "Job {JobId} of type {JobType} queued for tenant {TenantId}.")]
    public static partial void Queued(this ILogger logger, Guid jobId, string jobType, Guid tenantId);

    [LoggerMessage(EventId = 4101, Level = LogLevel.Debug, Message = "Job {JobId} claimed for attempt {Attempt} (state {State}).")]
    public static partial void Claimed(this ILogger logger, Guid jobId, int attempt, JobState state);
}
