using EpochsOfHumanity.Core.Prng;

namespace EpochsOfHumanity.Sim.Characters;

/// <summary>
/// Generates paleolithic-style names from a syllable corpus.
/// </summary>
/// <remarks>
/// Real Upper Paleolithic languages are entirely lost (deepest reconstructed
/// proto-languages reach only ~10,000 BCE). We use a deterministic phoneme
/// inventory of simple open syllables — consonant + vowel, no fricatives —
/// matching the suggested approach in <c>historical-research-paleolithic</c>
/// skill (§"Корпуса имён").
///
/// Pure function: same Rng state → same name. Used by SuccessionSystem to
/// generate names of heirs.
/// </remarks>
public static class NameGenerator
{
    // 12 simple CV syllables — broad cross-linguistic phoneme inventory
    private static readonly string[] Syllables =
    {
        "ta", "ka", "lu", "ne", "ma", "ru", "wa", "hu", "ki", "sa", "no", "te",
    };

    // Optional endings to add variety
    private static readonly string[] MaleEndings   = { "", "n", "k", "r", "h" };
    private static readonly string[] FemaleEndings = { "", "a", "i", "la", "na" };

    /// <summary>
    /// Generate a name deterministically from the given Rng.
    /// </summary>
    /// <remarks>
    /// Advances the Rng state by ~3-4 calls. Caller should pass an Rng forked
    /// for this specific purpose (e.g. <c>rng.Fork("heir-name-{tribeId}-{year}")</c>)
    /// so name generation is stable across replays and isolated from other
    /// subsystems (Law 1).
    /// </remarks>
    public static string Generate(Rng rng, Sex sex)
    {
        var nSyllables = rng.NextInt(2, 4); // 2 or 3
        var name = "";
        for (var i = 0; i < nSyllables; i++)
        {
            name += Syllables[rng.NextInt(0, Syllables.Length)];
        }

        var endings = sex == Sex.Male ? MaleEndings : FemaleEndings;
        name += endings[rng.NextInt(0, endings.Length)];

        // Capitalize first letter
        return char.ToUpperInvariant(name[0]) + name[1..];
    }
}
