using System;

namespace Serious.TestHelpers;

public class FakeStopwatchFactory : IStopwatchFactory
{
    public TimeSpan Elapsed { get; set; }

    public IStopwatch StartNew()
    {
        return new FakeStopwatch { Elapsed = Elapsed };
    }
}

public class FakeStopwatch : IStopwatch
{
    public TimeSpan Elapsed { get; init; }
    public long ElapsedMilliseconds => (long)Elapsed.TotalMilliseconds;
}
