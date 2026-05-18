using System.Collections.Generic;
using EpochsOfHumanity.Core.Prng;
using EpochsOfHumanity.Sim.State;

namespace EpochsOfHumanity.Sim.Characters;

/// <summary>
/// Annual chief mortality + heir generation. Deterministic, seeded.
/// </summary>
/// <remarks>
/// V0.1 placeholder dynasty: heirs have no real lineage — generated names + age.
/// V0.2 will replace with proper Dynasty trees (parent/child, marriage),
/// rooted in the CK3-style mechanics from <c>game-architecture</c> + Pillar 2.
///
/// Mortality model (paleolithic-plausible):
///   - Age &lt; 35: ~0.3% death/year (baseline + infection risk)
///   - Age 35-50: rising linearly to ~2%/year
///   - Age 50-65: rising to ~8%/year
///   - Age 65+: ~15%/year+ (very old in paleolithic context)
/// Very few chiefs survive past 70 winters.
/// </remarks>
public static class SuccessionSystem
{
    /// <summary>
    /// Process one in-game year. Returns the chief deaths that occurred and
    /// the heirs that replaced them. Mutates <paramref name="chiefsByTribe"/>
    /// in place.
    /// </summary>
    public static List<NarrativeEvent> ProcessYear(
        Dictionary<string, Chief> chiefsByTribe,
        int currentYearBP,
        Rng worldRng)
    {
        var events = new List<NarrativeEvent>();

        // Deterministic order — sort tribe ids ordinally (Law 1)
        var tribeIds = new List<string>(chiefsByTribe.Keys);
        tribeIds.Sort(System.StringComparer.Ordinal);

        foreach (var tribeId in tribeIds)
        {
            var chief = chiefsByTribe[tribeId];
            // Per-tribe-per-year PRNG, so subsystem changes elsewhere don't shift outcomes
            var rng = worldRng.Fork($"succession-{tribeId}-y{currentYearBP}");

            if (rng.Chance(DeathProbability(chief.AgeWinters)))
            {
                // Chief dies
                events.Add(new NarrativeEvent(
                    YearBP: currentYearBP,
                    TribeId: tribeId,
                    Kind: NarrativeEventKind.ChiefDied,
                    Message: $"Chief {chief.Name} of {tribeId} has died at {chief.AgeWinters} winters."));

                // Generate heir
                var heirRng = worldRng.Fork($"heir-{tribeId}-y{currentYearBP}");
                var heirSex = heirRng.Chance(0.5) ? Sex.Male : Sex.Female;
                var heirAge = heirRng.NextInt(14, 32); // young adult, ready to lead
                var heirName = NameGenerator.Generate(heirRng, heirSex);

                var heir = new Chief(heirName, heirSex, heirAge);
                chiefsByTribe[tribeId] = heir;

                events.Add(new NarrativeEvent(
                    YearBP: currentYearBP,
                    TribeId: tribeId,
                    Kind: NarrativeEventKind.HeirAscended,
                    Message: $"{heirName} ({heirAge} winters) became chief of {tribeId}."));
            }
            else
            {
                // Just age
                chiefsByTribe[tribeId] = chief with { AgeWinters = chief.AgeWinters + 1 };
            }
        }

        return events;
    }

    /// <summary>Death probability for one year at the given age. Smooth curve.</summary>
    public static double DeathProbability(int ageWinters)
    {
        // Hand-tuned piecewise-quadratic; not realistic actuarial — placeholder.
        if (ageWinters < 35) return 0.003;
        if (ageWinters < 50)
        {
            // 0.003 at 35 → 0.02 at 50
            var t = (ageWinters - 35) / 15.0;
            return 0.003 + (0.02 - 0.003) * t;
        }
        if (ageWinters < 65)
        {
            // 0.02 at 50 → 0.08 at 65
            var t = (ageWinters - 50) / 15.0;
            return 0.02 + (0.08 - 0.02) * t;
        }
        // 65+: ramps fast
        return System.Math.Min(0.5, 0.08 + (ageWinters - 65) * 0.03);
    }
}
