namespace EpochsOfHumanity.Sim.Characters;

/// <summary>
/// The leader of a tribe — placeholder for the full character model coming in v0.2.
/// </summary>
/// <remarks>
/// In v0.1 chiefs are static; in v0.2 they age, marry, have children, die →
/// dynasty mechanics (CK3-style). When a chief dies, the player switches to
/// the heir (game over only if dynasty extinct).
/// See <c>game-perception</c> skill for worldview integration coming in v0.4.
/// </remarks>
public sealed record Chief(
    string Name,        // Latin-transliterated invented name, e.g. "Tefnut", "Lala"
    Sex Sex,
    int AgeWinters);    // "winters" = years, paleolithic idiom
