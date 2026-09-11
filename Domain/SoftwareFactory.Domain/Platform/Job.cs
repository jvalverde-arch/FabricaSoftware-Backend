using SoftwareFactory.Domain.Common;

namespace SoftwareFactory.Domain.Platform;

/// <summary>Unit of asynchronous agent work queued in Postgres (doc 03, D6). The worker semantics arrive with T-007.</summary>
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
    }

    public string Type { get; private set; } = string.Empty;

    /// <summary>JSON document with the job arguments.</summary>
    public string Payload { get; private set; } = string.Empty;

    public JobState State { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }
}
