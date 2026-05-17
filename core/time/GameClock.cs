namespace EpochsOfHumanity.Core.Time;

/// <summary>
/// In-game time. Tracks strategic tick (1 season), tactical tick (1 hour),
/// year (in BP — Before Present, archaeological convention), and current season.
/// </summary>
/// <remarks>
/// Engine-agnostic (no Godot). Do NOT use <c>DateTime.Now</c>/<c>Stopwatch</c>
/// in simulation — only this clock. See <c>game-determinism</c> skill.
/// </remarks>
public sealed class GameClock
{
    /// <summary>Number of strategic ticks elapsed since world creation.</summary>
    public long StrategicTick { get; private set; }

    /// <summary>Number of tactical ticks elapsed (only advances when tactical layer is active).</summary>
    public long TacticalTick { get; private set; }

    /// <summary>Current in-game year, Before Present. Decreases as time progresses (45,000 → 44,999 → ...).</summary>
    public int YearBP { get; private set; }

    /// <summary>Current season.</summary>
    public Season Season { get; private set; }

    public GameClock(int startYearBP)
    {
        if (startYearBP <= 0)
            throw new System.ArgumentOutOfRangeException(
                nameof(startYearBP), "Start year must be positive (BP convention).");

        YearBP = startYearBP;
        Season = Season.Spring;
        StrategicTick = 0;
        TacticalTick = 0;
    }

    /// <summary>
    /// Advance strategic time by one tick = one season.
    /// Returns true if a new year has begun (Winter → Spring transition).
    /// </summary>
    public bool AdvanceStrategic()
    {
        StrategicTick++;
        Season = (Season)(((int)Season + 1) % 4);

        if (Season == Season.Spring)
        {
            YearBP--;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Advance tactical time by one tick = one in-game hour.
    /// Does not affect strategic tick or season — tactical layer runs faster, scoped to active settlement.
    /// </summary>
    public void AdvanceTactical()
    {
        TacticalTick++;
    }

    /// <summary>Internal state for save/load.</summary>
    public GameClockSnapshot ToSnapshot()
        => new(StrategicTick, TacticalTick, YearBP, Season);

    /// <summary>Restore from save.</summary>
    public static GameClock FromSnapshot(GameClockSnapshot s)
    {
        var clock = new GameClock(s.YearBP) // sets initial state
        {
            StrategicTick = s.StrategicTick,
            TacticalTick = s.TacticalTick,
            Season = s.Season,
        };
        return clock;
    }
}

public readonly record struct GameClockSnapshot(
    long StrategicTick,
    long TacticalTick,
    int YearBP,
    Season Season);
