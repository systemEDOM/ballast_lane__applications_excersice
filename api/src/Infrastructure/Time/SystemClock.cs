using BallastLane.Application.Abstractions;

namespace BallastLane.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}
