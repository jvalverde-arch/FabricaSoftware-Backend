using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Platform;

/// <summary>
/// Unit of asynchronous agent work queued in Postgres (doc 03, D6). A worker claims it, keeps a lease alive while it
/// runs, reports progress, and ends in success, retry with backoff, or definitive failure.
/// </summary>
public sealed class Job : TenantScopedEntity
{
    private Job()
    {
    }

    public Job(Guid tenantId, string type, string payload)
        : base(tenantId)
    {
        Type = Guard.NotBlank(type);
        Payload = Guard.ValidJson(payload);
        State = JobState.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        AvailableAt = CreatedAt;
    }

    public string Type { get; private set; } = string.Empty;

    /// <summary>JSON document with the job arguments.</summary>
    public string Payload { get; private set; } = string.Empty;

    public JobState State { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>Not claimable before this instant; backoff pushes it forward after a failure.</summary>
    public DateTimeOffset AvailableAt { get; private set; }

    /// <summary>While running, when the lease expires. A crashed worker leaves the job claimable again after it.</summary>
    public DateTimeOffset? LockedUntil { get; private set; }

    /// <summary>Human-readable phase of the run, shown by the progress stream («leyendo contexto», «generando», ...).</summary>
    public string? Phase { get; private set; }

    public int? ProgressPercent { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsFinished => State is JobState.Succeeded or JobState.Failed or JobState.Cancelled;

    /// <summary>Claims the job for a run: counts the attempt and takes a lease for <paramref name="lease"/>.</summary>
    public void Start(DateTimeOffset now, TimeSpan lease)
    {
        if (State != JobState.Pending)
        {
            throw new InvalidOperationException($"Only a pending job can start; this one is {State}.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lease, TimeSpan.Zero);

        State = JobState.Running;
        Attempts++;
        StartedAt = now.ToUniversalTime();
        LockedUntil = StartedAt.Value.Add(lease);
        LastError = null;
        Touch(now);
    }

    public void ReportProgress(string phase, int? percent, DateTimeOffset now, TimeSpan lease)
    {
        RequireRunning();

        if (percent is { } value)
        {
            Guard.InRange(value, 0, 100);
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lease, TimeSpan.Zero);

        Phase = Guard.NotBlank(phase);
        ProgressPercent = percent ?? ProgressPercent;
        // Reporting progress renews the lease: whoever is working is alive.
        LockedUntil = now.ToUniversalTime().Add(lease);
        Touch(now);
    }

    public void Succeed(DateTimeOffset now)
    {
        RequireRunning();

        State = JobState.Succeeded;
        ProgressPercent = 100;
        CompletedAt = now.ToUniversalTime();
        LockedUntil = null;
        LastError = null;
        Touch(now);
    }

    /// <summary>
    /// Records a failed run. Returns true when the job goes back to the queue with backoff, false when it has spent
    /// every attempt and is failed for good.
    /// </summary>
    public bool Fail(string error, DateTimeOffset now, JobRetryPolicy policy)
    {
        RequireRunning();
        ArgumentNullException.ThrowIfNull(policy);

        LastError = Guard.NotBlank(error);
        LockedUntil = null;

        if (Attempts >= policy.MaxAttempts)
        {
            State = JobState.Failed;
            CompletedAt = now.ToUniversalTime();
            Touch(now);
            return false;
        }

        State = JobState.Pending;
        AvailableAt = now.ToUniversalTime().Add(policy.DelayAfter(Attempts));
        Touch(now);
        return true;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (IsFinished)
        {
            throw new InvalidOperationException($"A {State} job cannot be cancelled.");
        }

        State = JobState.Cancelled;
        CompletedAt = now.ToUniversalTime();
        LockedUntil = null;
        Touch(now);
    }

    public bool IsLeaseExpired(DateTimeOffset now) => State == JobState.Running && LockedUntil is { } until && until < now;

    /// <summary>
    /// Returns a job whose worker died to the queue. The attempt it already spent still counts, so a job that keeps
    /// killing its worker eventually exhausts its attempts instead of looping forever.
    /// </summary>
    public void ReleaseExpiredLease(DateTimeOffset now)
    {
        if (State != JobState.Running)
        {
            throw new InvalidOperationException($"Only a running job holds a lease; this one is {State}.");
        }

        State = JobState.Pending;
        AvailableAt = now.ToUniversalTime();
        LockedUntil = null;
        Touch(now);
    }

    private void RequireRunning()
    {
        if (State != JobState.Running)
        {
            throw new InvalidOperationException($"The job must be running for this; it is {State}.");
        }
    }

    private void Touch(DateTimeOffset now) => UpdatedAt = now.ToUniversalTime();
}
