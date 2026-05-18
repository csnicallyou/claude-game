using System.Collections.Generic;
using EpochsOfHumanity.Sim.Characters;
using EpochsOfHumanity.Sim.State;

namespace EpochsOfHumanity.Core.Save;

/// <summary>
/// JSON-serializable snapshot of the game state.
/// </summary>
/// <remarks>
/// FormatVersion increments on any change to the schema. V0.5 will add proper
/// migrations (CLAUDE.md §3 / interview block 7.1.1). Until then, bumping the
/// version invalidates older saves with a clear error.
///
/// v1 → v2: switched time unit from years to seasons (more granular).
/// </remarks>
public sealed record GameSaveState(
    int FormatVersion,
    string SaveId,
    string SavedAtIso,
    string Seed,
    int StartYearBP,
    long SeasonsElapsed,
    Dictionary<string, Chief> Chiefs,
    List<NarrativeEvent> Events)
{
    public const int CurrentFormatVersion = 2;
}
