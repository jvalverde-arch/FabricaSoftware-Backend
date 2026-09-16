namespace SoftwareFactory.Application.Common.Time;

/// <summary>
/// Monotonic time source for measuring durations. <see cref="TimeProvider"/> gives the wall clock, which can jump;
/// latency must come from a stopwatch.
/// </summary>
public interface IMonotonicClock
{
    long GetTimestamp();

    TimeSpan GetElapsed(long startingTimestamp);
}
