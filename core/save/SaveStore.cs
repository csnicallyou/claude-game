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
            Chiefs: new System.Collections.Generic.Dictionary<string, Chief>(state.Chiefs, System.StringComparer.Ordinal),
            Events: new System.Collections.Generic.List<NarrativeEvent>(state.AllEvents));
    }

    public static GameState FromSnapshot(GameSaveState snapshot, TribeRegistry initialTribes)
    {
        return new GameState(
            seed: snapshot.Seed,
            initialTribes: initialTribes,
            startYearBP: snapshot.StartYearBP,
            seasonsElapsed: snapshot.SeasonsElapsed,
            chiefs: snapshot.Chiefs,
            events: snapshot.Events);
    }
}
