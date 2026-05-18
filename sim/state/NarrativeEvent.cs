namespace EpochsOfHumanity.Sim.State;

/// <summary>
/// A short narrative beat — chief death, succession, future major events.
/// Surfaced by the simulation, displayed by the render layer.
/// </summary>
/// <remarks>
/// V0.1: simple text message + tribe id. V0.4+ will route these through
/// the perception layer (Law 4) so each character sees them through their
/// worldview lens.
/// </remarks>
public sealed record NarrativeEvent(
    int YearBP,
    string TribeId,
    NarrativeEventKind Kind,
    string Message);

public enum NarrativeEventKind
{
    ChiefDied = 0,
    HeirAscended = 1,
    DynastyExtinct = 2,
}
