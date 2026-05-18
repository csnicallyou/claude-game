using System.Collections.Generic;
using EpochsOfHumanity.Core.Prng;
using EpochsOfHumanity.Core.Time;
using EpochsOfHumanity.Sim.Characters;

namespace EpochsOfHumanity.Sim.State;

/// <summary>
/// Mutable game state holder. Owns time (in seasons), current chiefs,
/// narrative log. Engine-agnostic (Law 2 — no Godot).
/// </summary>
/// <remarks>
/// V0.2: time unit is the season (1/4 year). Year advances when transitioning
/// into Spring. Succession (chief aging + death) runs once per year, at the
/// Spring transition. CLAUDE.md §4.1 maps real seconds → seasons per speed.
/// </remarks>
public sealed class GameState
{
    public const int DefaultStartYearBP = 45_000;

    public string Seed { get; }
    public Rng Rng { get; }
    public TribeRegistry InitialTribes { get; }
    public int StartYearBP { get; }

    /// <summary>Seasons since game start. 4 seasons = 1 year.</summary>
    public long SeasonsElapsed { get; private set; }

    /// <summary>Years since game start (derived from <see cref="SeasonsElapsed"/>).</summary>
    public int YearsElapsed => (int)(SeasonsElapsed / 4);

    /// <summary>Current in-game year, Before Present convention. Starts large, decreases.</summary>
    public int CurrentYearBP => StartYearBP - YearsElapsed;

    /// <summary>Current season — Spring at game start, derived from SeasonsElapsed.</summary>
    public Season CurrentSeason => (Season)((int)(SeasonsElapsed % 4));

    private readonly Dictionary<string, Chief> _chiefs;
    private readonly List<NarrativeEvent> _allEvents = new();

    public IReadOnlyList<NarrativeEvent> AllEvents => _allEvents;

    /// <summary>Events emitted on the most recent season tick. Empty between year transitions.</summary>
    public IReadOnlyList<NarrativeEvent> LatestEvents { get; private set; }
        = System.Array.Empty<NarrativeEvent>();

    public GameState(string seed, TribeRegistry initialTribes, int startYearBP = DefaultStartYearBP)
    {
        Seed = seed;
        Rng = new Rng(seed);
        InitialTribes = initialTribes;
        StartYearBP = startYearBP;
        SeasonsElapsed = 0;

        _chiefs = new Dictionary<string, Chief>(System.StringComparer.Ordinal);
        foreach (var t in initialTribes.All)
            _chiefs[t.Id] = t.Chief;
    }

    /// <summary>Restore from snapshot (used after Load).</summary>
    public GameState(
        string seed,
        TribeRegistry initialTribes,
        int startYearBP,
        long seasonsElapsed,
        Dictionary<string, Chief> chiefs,
        IReadOnlyList<NarrativeEvent> events)
    {
        Seed = seed;
        Rng = new Rng(seed);
        InitialTribes = initialTribes;
        StartYearBP = startYearBP;
        SeasonsElapsed = seasonsElapsed;
        _chiefs = new Dictionary<string, Chief>(chiefs, System.StringComparer.Ordinal);
        _allEvents = new List<NarrativeEvent>(events);
    }

    public Chief ChiefOf(string tribeId) => _chiefs[tribeId];
    public IReadOnlyDictionary<string, Chief> Chiefs => _chiefs;

    /// <summary>
    /// Advance one season. On Spring transitions (full year completed),
    /// runs succession (chief aging + death + heir).
    /// </summary>
    public void AdvanceSeason()
    {
        SeasonsElapsed++;
        // After increment, if we landed exactly on a Spring boundary (i.e. SeasonsElapsed % 4 == 0),
        // a year has completed. Note: SeasonsElapsed=0 (game start) means Spring but no succession yet —
        // we only run succession after increment.
        if (SeasonsElapsed % 4 == 0)
        {
            var events = SuccessionSystem.ProcessYear(_chiefs, CurrentYearBP, Rng);
            LatestEvents = events;
            _allEvents.AddRange(events);
        }
        else
        {
            LatestEvents = System.Array.Empty<NarrativeEvent>();
        }
    }

    /// <summary>Convenience: advance four seasons (= one full year).</summary>
    public void AdvanceYear()
    {
        for (var i = 0; i < 4; i++) AdvanceSeason();
    }
}
