using System.Collections.Generic;
using EpochsOfHumanity.Sim.Characters;
using EpochsOfHumanity.Sim.State;

namespace EpochsOfHumanity.Core.Save;

/// <summary>
/// JSON-serializable snapshot of the game state.
/// </summary>
/// <remarks>
/// FormatVersion increments on any schema change. V0.5 will add proper migrations
/// (CLAUDE.md §3 / interview block 7.1.1); until then, bumping the version
/// invalidates older saves with a clear error.
///
/// Version history:
///   v1 — initial: YearsElapsed, Chiefs, Events.
///   v2 — switched time to seasons (SeasonsElapsed).
///   v3 — added per-tribe population counts (Pops).
/// </remarks>
public sealed record GameSaveState(
    int FormatVersion,
    string SaveId,
    string SavedAtIso,
    string Seed,
    int StartYearBP,
    long SeasonsElapsed,
    Dictionary<string, Chief> Chiefs,
    Dictionary<string, int> Pops,
    List<NarrativeEvent> Events)
{
    public const int CurrentFormatVersion = 3;
}
