using SoftwareFactory.Application.Common.Time;

namespace SoftwareFactory.Application.Tests.Finops.Fakes;

/// <summary>Latency is measured with a monotonic stopwatch; this one advances by a fixed step per call.</summary>
internal sealed class FakeMonotonicClock(TimeSpan step) : IMonotonicClock
{
    private long _ticks;

    public long GetTimestamp()
    {
        var current = _ticks;
        _ticks += step.Ticks;
        return current;
    }

    public TimeSpan GetElapsed(long startingTimestamp) => TimeSpan.FromTicks(_ticks - startingTimestamp);
}
