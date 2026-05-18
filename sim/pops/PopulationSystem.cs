using System.Collections.Generic;
using EpochsOfHumanity.Core.Prng;
using EpochsOfHumanity.Sim.Characters;

namespace EpochsOfHumanity.Sim.Pops;

/// <summary>
/// Per-tribe population system. V0.2 minimum: pop = single number per tribe,
/// grows/shrinks each year based on biome productivity and crowding pressure.
/// </summary>
/// <remarks>
/// This is the "pop aggregate" half of the two-layer simulation (CLAUDE.md §3,
/// Закон 4 / interview block 3.3). In v0.3+ the player's own tribe gains
/// individual-level sim (~500 named indivs); other tribes stay as pops.
///
/// Deterministic: per-tribe-per-year forked PRNG isolates pop changes from
/// other subsystems (Law 1).
///
/// Mortality of pops is implicit — we don't track births/deaths explicitly,
/// just net change. Chief death runs separately in SuccessionSystem.
/// </remarks>
public static class PopulationSystem
{
    /// <summary>Lower bound: tribes don't go extinct in v0.2 (deferred to v0.3+).</summary>
    public const int FloorPop = 5;

    /// <summary>Generate an initial population size for a starting tribe.</summary>
    public static int InitialPopFor(Species species, Rng rng)
    {
        var (lo, hi) = species switch
        {
            Species.Sapiens     => (25, 46),  // larger groups, social network bigger
            Species.Neanderthal => (18, 33),  // tighter groups, anatomical adapted
            Species.Denisovan   => (20, 35),
            _                   => (20, 35),
        };
        return rng.NextInt(lo, hi);
    }

    /// <summary>
    /// Run one year of population dynamics. Mutates <paramref name="pops"/> in place.
    /// Should be called on Spring transition only.
    /// </summary>
    public static void ProcessYear(
        Dictionary<string, int> pops,
        IReadOnlyDictionary<string, double> tribeProductivity,
        IEnumerable<string> tribeIds,
        int currentYearBP,
        Rng worldRng)
    {
        // Deterministic order (Law 1)
        var sorted = new List<string>(tribeIds);
        sorted.Sort(System.StringComparer.Ordinal);

        foreach (var tribeId in sorted)
        {
            if (!pops.TryGetValue(tribeId, out var pop)) continue;
            if (!tribeProductivity.TryGetValue(tribeId, out var productivity)) productivity = 0.5;

            var rng = worldRng.Fork($"pop-{tribeId}-y{currentYearBP}");
            pops[tribeId] = ComputeNextPop(pop, productivity, rng);
        }
    }

    /// <summary>
    /// Pure function: given current pop, productivity 0..1, and per-tribe rng,
    /// returns next year's pop. Hard floor at <see cref="FloorPop"/>.
    /// </summary>
    public static int ComputeNextPop(int pop, double productivity, Rng rng)
    {
        // Carrying capacity scales with productivity. 1.0 productivity = 80 max pop
        // (paleolithic band-level, real ethnographic range).
        var capacity = productivity * 80.0;

        // Food index: noisy environmental contribution
        var foodIndex = productivity * (1.0 + (rng.NextDouble() - 0.5) * 0.2);
        // Crowding pressure: pop ÷ capacity. >1 = overcrowded → starvation.
        var crowding = capacity > 0 ? pop / capacity : double.MaxValue;

        // Growth rate per year. Roughly:
        //   pop << capacity → positive (up to +5%)
        //   pop ≈ capacity → small (paleolithic equilibrium is near capacity)
        //   pop > capacity → negative (starvation, up to -12%)
        var growthRate = (foodIndex - crowding) * 0.05;
        growthRate = System.Math.Clamp(growthRate, -0.12, 0.05);

        var next = (int)System.Math.Round(pop * (1.0 + growthRate));
        if (next < FloorPop) next = FloorPop;
        return next;
    }
}
