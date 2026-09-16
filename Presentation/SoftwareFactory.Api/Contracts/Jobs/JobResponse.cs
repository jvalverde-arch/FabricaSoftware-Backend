using SoftwareFactory.Application.Platform.Contracts;

namespace SoftwareFactory.Api.Contracts.Jobs;

/// <summary>State of a run as the SPA sees it; also the payload of every event of the progress stream.</summary>
public sealed record JobResponse(
    Guid Id,
    string Type,
    string State,
    string? Phase,
    int? ProgressPercent,
    int Attempts,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt)
{
    public static JobResponse From(JobSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new JobResponse(
            snapshot.Id,
            snapshot.Type,
            JobStateNames.Of(snapshot.State),
            snapshot.Phase,
            snapshot.ProgressPercent,
            snapshot.Attempts,
            snapshot.LastError,
            snapshot.CreatedAt,
            snapshot.UpdatedAt,
            snapshot.StartedAt,
            snapshot.CompletedAt);
    }
}
