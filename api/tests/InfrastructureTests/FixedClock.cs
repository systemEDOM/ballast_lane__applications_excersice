using BallastLane.Application.Abstractions;

namespace BallastLane.InfrastructureTests;

internal sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; }

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}
