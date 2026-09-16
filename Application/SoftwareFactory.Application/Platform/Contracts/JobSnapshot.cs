using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Platform.Contracts;

/// <summary>Read model of a run: what the progress stream and the job endpoint show.</summary>
public sealed record JobSnapshot(
    Guid Id,
    string Type,
    JobState State,
    string? Phase,
    int? ProgressPercent,
    int Attempts,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt)
{
    public bool IsFinished => State is JobState.Succeeded or JobState.Failed or JobState.Cancelled;

    public static JobSnapshot Of(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);

        return new JobSnapshot(
            job.Id,
            job.Type,
            job.State,
            job.Phase,
            job.ProgressPercent,
            job.Attempts,
            job.LastError,
            job.CreatedAt,
            job.UpdatedAt,
            job.StartedAt,
            job.CompletedAt);
    }
}
