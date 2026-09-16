namespace SoftwareFactory.Api.Jobs;

/// <summary>
/// Progress stream settings (section <c>Jobs:Stream</c>). The stream is a long-lived response: it must not outlive
/// the client nor the run, so it has a heartbeat that detects a gone client and a hard ceiling.
/// </summary>
public sealed class JobStreamOptions
{
    public const string SectionName = "Jobs:Stream";

    /// <summary>How often the row is re-read. Events are only sent when something actually changed.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Comment sent when nothing changed, so a dead connection is discovered instead of lingering.</summary>
    public TimeSpan Heartbeat { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Ceiling for one connection; the client reconnects if the run is still going.</summary>
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromMinutes(30);

    public bool IsValid() => PollInterval > TimeSpan.Zero && Heartbeat >= PollInterval && MaxDuration > TimeSpan.Zero;
}
