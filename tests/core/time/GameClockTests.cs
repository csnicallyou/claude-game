using EpochsOfHumanity.Core.Time;
using Xunit;

namespace EpochsOfHumanity.Tests.Core.Time;

public class GameClockTests
{
    [Fact]
    public void StartsAt_SpringOfStartYear_TickZero()
    {
        var clock = new GameClock(startYearBP: 45_000);

        Assert.Equal(45_000, clock.YearBP);
        Assert.Equal(Season.Spring, clock.Season);
        Assert.Equal(0L, clock.StrategicTick);
        Assert.Equal(0L, clock.TacticalTick);
    }

    [Fact]
    public void AdvanceStrategic_CyclesSeasons()
    {
        var clock = new GameClock(45_000);

        Assert.False(clock.AdvanceStrategic());
        Assert.Equal(Season.Summer, clock.Season);

        Assert.False(clock.AdvanceStrategic());
        Assert.Equal(Season.Autumn, clock.Season);

        Assert.False(clock.AdvanceStrategic());
        Assert.Equal(Season.Winter, clock.Season);

        // Winter → Spring of next year — should return true
        var newYear = clock.AdvanceStrategic();
        Assert.True(newYear);
        Assert.Equal(Season.Spring, clock.Season);
        Assert.Equal(44_999, clock.YearBP);
    }

    [Fact]
    public void YearBP_DecreasesAsTimeProgresses()
    {
        var clock = new GameClock(45_000);

        // 4 seasons = 1 year
        for (var i = 0; i < 100 * 4; i++) clock.AdvanceStrategic();

        Assert.Equal(45_000 - 100, clock.YearBP);
        Assert.Equal(Season.Spring, clock.Season);
        Assert.Equal(400L, clock.StrategicTick);
    }

    [Fact]
    public void Snapshot_RoundTrips()
    {
        var clock = new GameClock(45_000);
        for (var i = 0; i < 13; i++) clock.AdvanceStrategic(); // 3 years + 1 season

        var snap = clock.ToSnapshot();
        var restored = GameClock.FromSnapshot(snap);

        Assert.Equal(clock.YearBP, restored.YearBP);
        Assert.Equal(clock.Season, restored.Season);
        Assert.Equal(clock.StrategicTick, restored.StrategicTick);
        Assert.Equal(clock.TacticalTick, restored.TacticalTick);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveYear()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new GameClock(0));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new GameClock(-1));
    }
}
