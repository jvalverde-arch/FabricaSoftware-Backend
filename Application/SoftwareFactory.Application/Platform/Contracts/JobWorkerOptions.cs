using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Platform.Contracts;

/// <summary>Settings of the worker loop (doc 03, D6). Bound from the <c>Jobs</c> configuration section.</summary>
public sealed class JobWorkerOptions
{
    public const string SectionName = "Jobs";

    /// <summary>Wait between polls when the queue came back empty.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>How long a claim is good for; a worker that dies frees the job after this.</summary>
    public TimeSpan Lease { get; set; } = TimeSpan.FromMinutes(5);

    public int MaxAttempts { get; set; } = 3;

    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Set to false to keep the worker idle (useful in hosts that only serve requests).</summary>
    public bool Enabled { get; set; } = true;

    public JobRetryPolicy RetryPolicy() => new(MaxAttempts, RetryBaseDelay, RetryMaxDelay);

    public bool IsValid() => PollInterval > TimeSpan.Zero && Lease > TimeSpan.Zero && RetryPolicy().IsValid();
}
