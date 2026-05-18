using System.Collections.Generic;
using EpochsOfHumanity.Core.Prng;
using EpochsOfHumanity.Sim.Characters;

namespace EpochsOfHumanity.Sim.State;

/// <summary>
/// Mutable game state holder. Owns time, current chiefs, narrative log.
/// Engine-agnostic (Law 2 — no Godot).
/// </summary>
/// <remarks>
/// V0.1: lightweight, just chiefs + year. V0.2 will be replaced by the full
/// GameWorld + ECS + Command queue. This is the bridge between the static
/// initial state (LevantTribesPreset) and the eventual proper simulation.
/// </remarks>
public sealed class GameState
{
    public const int DefaultStartYearBP = 45_000;

    public string Seed { get; }
    public Rng Rng { get; }
    public TribeRegistry InitialTribes { get; }
    public int StartYearBP { get; }
    public int YearsElapsed { get; private set; }
    public int CurrentYearBP => StartYearBP - YearsElapsed;

    private readonly Dictionary<string, Chief> _chiefs;
    private readonly List<NarrativeEvent> _allEvents = new();

    public IReadOnlyList<NarrativeEvent> AllEvents => _allEvents;

    /// <summary>Events emitted in the most recent year tick.</summary>
    public IReadOnlyList<NarrativeEvent> LatestEvents { get; private set; }
        = System.Array.Empty<NarrativeEvent>();

    public GameState(string seed, TribeRegistry initialTribes, int startYearBP = DefaultStartYearBP)
    {
        Seed = seed;
        Rng = new Rng(seed);
        InitialTribes = initialTribes;
        StartYearBP = startYearBP;
        YearsElapsed = 0;

        _chiefs = new Dictionary<string, Chief>(System.StringComparer.Ordinal);
        foreach (var t in initialTribes.All)
            _chiefs[t.Id] = t.Chief;
    }

    /// <summary>Restore from snapshot (used after Load).</summary>
    public GameState(
        string seed,
        TribeRegistry initialTribes,
        int startYearBP,
        int yearsElapsed,
        Dictionary<string, Chief> chiefs,
        IReadOnlyList<NarrativeEvent> events)
    {
        Seed = seed;
        Rng = new Rng(seed);
        // Advance Rng to match where it would be after yearsElapsed AdvanceYears.
        // For determinism we re-derive via the same Fork-paths during succession,
        // so a plain seeded Rng is fine here (each year creates fresh forks).
        InitialTribes = initialTribes;
        StartYearBP = startYearBP;
        YearsElapsed = yearsElapsed;
        _chiefs = new Dictionary<string, Chief>(chiefs, System.StringComparer.Ordinal);
        _allEvents = new List<NarrativeEvent>(events);
    }

    public Chief ChiefOf(string tribeId) => _chiefs[tribeId];

    public IReadOnlyDictionary<string, Chief> Chiefs => _chiefs;

    /// <summary>Advance one in-game year. Processes aging + succession.</summary>
    public void AdvanceYear()
    {
        YearsElapsed++;
        var newEvents = SuccessionSystem.ProcessYear(_chiefs, CurrentYearBP, Rng);
        LatestEvents = newEvents;
        _allEvents.AddRange(newEvents);
    }
}
