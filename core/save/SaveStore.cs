using System.Collections.Generic;
using EpochsOfHumanity.Sim.Characters;
using EpochsOfHumanity.Sim.State;

namespace EpochsOfHumanity.Core.Save;

/// <summary>
/// Converts between the live <see cref="GameState"/> and its serialized snapshot.
/// File I/O is the render layer's responsibility (Godot's FileAccess), not here.
/// </summary>
public static class SaveStore
{
    public static GameSaveState ToSnapshot(GameState state, string saveId)
    {
        return new GameSaveState(
            FormatVersion: GameSaveState.CurrentFormatVersion,
            SaveId: saveId,
            SavedAtIso: System.DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            Seed: state.Seed,
            StartYearBP: state.StartYearBP,
            SeasonsElapsed: state.SeasonsElapsed,
            Chiefs: new Dictionary<string, Chief>(state.Chiefs, System.StringComparer.Ordinal),
            Pops: new Dictionary<string, int>(state.Pops, System.StringComparer.Ordinal),
            Events: new List<NarrativeEvent>(state.AllEvents));
    }

    /// <summary>
    /// Restore a GameState from snapshot. <paramref name="tribeProductivity"/> is
    /// re-derived by the caller from the current biome registry (not saved — fresh
    /// data takes precedence per <c>game-state</c> skill §"Что НЕ хранить в сейве").
    /// </summary>
    public static GameState FromSnapshot(
        GameSaveState snapshot,
        TribeRegistry initialTribes,
        IReadOnlyDictionary<string, double> tribeProductivity)
    {
        return new GameState(
            seed: snapshot.Seed,
            initialTribes: initialTribes,
            tribeProductivity: tribeProductivity,
            startYearBP: snapshot.StartYearBP,
            seasonsElapsed: snapshot.SeasonsElapsed,
            chiefs: snapshot.Chiefs,
            pops: snapshot.Pops,
            events: snapshot.Events);
    }
}
