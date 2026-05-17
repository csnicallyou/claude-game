namespace EpochsOfHumanity.Sim.Characters;

/// <summary>
/// Hominid species playable or present at the game's start (45,000 BCE Levant).
/// </summary>
/// <remarks>
/// See <c>historical-research-paleolithic</c> skill for the full list of hominids
/// alive globally at 45k BCE. In our MVP region (Levant), only Sapiens and
/// Neanderthal are realistic full presences; Denisovan appears as rare wanderer.
/// </remarks>
public enum Species
{
    Sapiens = 0,
    Neanderthal = 1,
    Denisovan = 2,
    // Floresiensis, Luzonensis, ErectusRelict — post-MVP, other regions
}
