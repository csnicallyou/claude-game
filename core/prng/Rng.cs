namespace EpochsOfHumanity.Core.Prng;

/// <summary>
/// Seeded, deterministic pseudo-random number generator.
/// </summary>
/// <remarks>
/// SplitMix64 algorithm — stable across runs, machines and .NET versions.
/// One instance per simulation world. Subsystems get child PRNGs via <see cref="Fork"/>
/// so changes in one subsystem do not shift randomness in another.
///
/// Forbidden in core/sim/eras: <c>System.Random</c>, <c>Random.Shared</c>,
/// <c>Godot.GD.Randf</c>, <c>Godot.RandomNumberGenerator</c> — use this instead.
/// See <c>.claude/skills/game-determinism/SKILL.md</c>.
/// </remarks>
public sealed class Rng
{
    private ulong _state;

    public Rng(string seed) : this(StableHash.Of(seed))
    {
    }

    public Rng(ulong state)
    {
        // Avoid zero state (SplitMix64 doesn't degenerate at zero, but be explicit)
        _state = state == 0UL ? 0xCAFEBABEDEADBEEFUL : state;
    }

    /// <summary>The internal state. Exposed for save/load only; do not read in game logic.</summary>
    public ulong State => _state;

    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform <c>double</c> in [0, 1). 53 bits of randomness.</summary>
    public double NextDouble()
        => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Uniform <c>int</c> in [minInclusive, maxExclusive).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new System.ArgumentException(
                $"maxExclusive ({maxExclusive}) must be > minInclusive ({minInclusive})");

        var range = (uint)(maxExclusive - minInclusive);
        return minInclusive + (int)(NextUInt64() % range);
    }

    /// <summary>Bernoulli trial: returns true with probability <paramref name="p"/>.</summary>
    public bool Chance(double p)
    {
        if (p <= 0.0) return false;
        if (p >= 1.0) return true;
        return NextDouble() < p;
    }

    /// <summary>
    /// Creates a child PRNG deterministically derived from this one and a subsystem name.
    /// Changes in one subsystem (e.g. climate) do not shift randomness in another (e.g. ai).
    /// </summary>
    /// <remarks>
    /// Does NOT advance the parent's state — forking is read-only with respect to parent.
    /// Two forks with the same name yield identical child states.
    /// </remarks>
    public Rng Fork(string subsystemName)
    {
        var nameHash = StableHash.Of(subsystemName);
        var childState = StableHash.Combine(_state, nameHash);
        return new Rng(childState);
    }
}
