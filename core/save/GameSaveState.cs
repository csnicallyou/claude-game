using System.Collections.Generic;
using EpochsOfHumanity.Sim.Characters;
using EpochsOfHumanity.Sim.State;

namespace EpochsOfHumanity.Core.Save;

/// <summary>
/// JSON-serializable snapshot of the game state. V0.1 minimum format.
/// </summary>
/// <remarks>
/// FormatVersion increments on any change to the schema. V0.5 will add proper
/// migrations (see CLAUDE.md §3 — block 7.1.1). Until then, bumping the version
/// invalidates older saves with a clear error.
/// </remarks>
public sealed record GameSaveState(
    int FormatVersion,
    string SaveId,
    string SavedAtIso,
    string Seed,
    int StartYearBP,
    int YearsElapsed,
    Dictionary<string, Chief> Chiefs,
    List<NarrativeEvent> Events)
{
    public const int CurrentFormatVersion = 1;
}
