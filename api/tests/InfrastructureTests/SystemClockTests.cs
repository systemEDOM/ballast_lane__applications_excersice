using BallastLane.Infrastructure.Time;
using Shouldly;
using Xunit;

namespace BallastLane.InfrastructureTests;

public sealed class SystemClockTests
{
    [Fact]
    public void UtcNowAndToday_ReturnCurrentUtcValues()
    {
        var clock = new SystemClock();
        var before = DateTimeOffset.UtcNow;
        var now = clock.UtcNow;
        var after = DateTimeOffset.UtcNow;

        now.ShouldBeGreaterThanOrEqualTo(before);
        now.ShouldBeLessThanOrEqualTo(after);
        now.Offset.ShouldBe(TimeSpan.Zero);
        clock.Today.ShouldBe(DateOnly.FromDateTime(now.UtcDateTime));
    }
}
