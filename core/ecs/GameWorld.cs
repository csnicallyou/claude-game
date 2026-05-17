using EpochsOfHumanity.Core.Prng;
using EpochsOfHumanity.Core.Time;

namespace EpochsOfHumanity.Core.Ecs;

/// <summary>
/// The simulation world. Holds clock, PRNG and (in future versions) the Arch ECS.
/// </summary>
/// <remarks>
/// Engine-agnostic (Law 2: no Godot dependency). Created once per game, seeded
/// at start for determinism (Law 1). This is currently a stub; Arch ECS integration
/// arrives in v0.1 once we wire the first simulation system.
///
/// Subsystems should always get their PRNG via <c>world.Rng.Fork("subsystem-name")</c>
/// and store the result, not call <c>world.Rng</c> directly during ticks.
/// </remarks>
public sealed class GameWorld
{
    public string Seed { get; }
    public Rng Rng { get; }
    public GameClock Clock { get; }

    public GameWorld(string seed, int startYearBP = 45_000)
    {
        Seed = seed ?? throw new System.ArgumentNullException(nameof(seed));
        Rng = new Rng(seed);
        Clock = new GameClock(startYearBP);
    }

    /// <summary>Used only for save loading.</summary>
    internal GameWorld(string seed, ulong rngState, GameClockSnapshot clockSnapshot)
    {
        Seed = seed;
        Rng = new Rng(rngState);
        Clock = GameClock.FromSnapshot(clockSnapshot);
    }
}
