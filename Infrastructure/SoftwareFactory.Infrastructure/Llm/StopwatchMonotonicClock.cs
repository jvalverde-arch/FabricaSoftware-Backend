using System.Diagnostics;
using SoftwareFactory.Application.Common.Time;

namespace SoftwareFactory.Infrastructure.Llm;

/// <summary>Latency measured with the high-resolution counter, which never jumps backwards like the wall clock.</summary>
internal sealed class StopwatchMonotonicClock : IMonotonicClock
{
    public long GetTimestamp() => Stopwatch.GetTimestamp();

    public TimeSpan GetElapsed(long startingTimestamp) => Stopwatch.GetElapsedTime(startingTimestamp);
}
