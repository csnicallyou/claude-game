using System.Collections.Generic;
using EpochsOfHumanity.Core.Prng;
using EpochsOfHumanity.Core.Time;
using EpochsOfHumanity.Sim.Characters;
using EpochsOfHumanity.Sim.Pops;

namespace EpochsOfHumanity.Sim.State;

/// <summary>
/// Mutable game state holder. Owns time (in seasons), current chiefs, populations,
/// narrative log. Engine-agnostic (Law 2 — no Godot).
/// </summary>
public sealed class GameState
{
    public const int DefaultStartYearBP = 45_000;

    public string Seed { get; }
    public Rng Rng { get; }
    public TribeRegistry InitialTribes { get; }
    public int StartYearBP { get; }

    public long SeasonsElapsed { get; private set; }
    public int YearsElapsed => (int)(SeasonsElapsed / 4);
    public int CurrentYearBP => StartYearBP - YearsElapsed;
    public Season CurrentSeason => (Season)((int)(SeasonsElapsed % 4));

    /// <summary>Per-tribe productivity (0..1), typically derived from home-hex biome.</summary>
    public IReadOnlyDictionary<string, double> TribeProductivity { get; }

    private readonly Dictionary<string, Chief> _chiefs;
    private readonly Dictionary<string, int> _pops;
    private readonly List<NarrativeEvent> _allEvents = new();

    public Chief ChiefOf(string tribeId) => _chiefs[tribeId];
    public IReadOnlyDictionary<string, Chief> Chiefs => _chiefs;

    public int PopOf(string tribeId) => _pops[tribeId];
    public IReadOnlyDictionary<string, int> Pops => _pops;

    public IReadOnlyList<NarrativeEvent> AllEvents => _allEvents;
    public IReadOnlyList<NarrativeEvent> LatestEvents { get; private set; }
        = System.Array.Empty<NarrativeEvent>();

    public GameState(
        string seed,
        TribeRegistry initialTribes,
        IReadOnlyDictionary<string, double>? tribeProductivity = null,
        int startYearBP = DefaultStartYearBP)
    {
        Seed = seed;
        Rng = new Rng(seed);
        InitialTribes = initialTribes;
        StartYearBP = startYearBP;
        SeasonsElapsed = 0;

        TribeProductivity = tribeProductivity ?? FallbackProductivity(initialTribes);

        _chiefs = new Dictionary<string, Chief>(System.StringComparer.Ordinal);
        _pops = new Dictionary<string, int>(System.StringComparer.Ordinal);
        foreach (var t in initialTribes.All)
        {
            _chiefs[t.Id] = t.Chief;
            var popRng = Rng.Fork($"initial-pop-{t.Id}");
            _pops[t.Id] = PopulationSystem.InitialPopFor(t.Species, popRng);
        }
    }

    /// <summary>Restore from snapshot.</summary>
    public GameState(
        string seed,
        TribeRegistry initialTribes,
        IReadOnlyDictionary<string, double> tribeProductivity,
        int startYearBP,
        long seasonsElapsed,
        Dictionary<string, Chief> chiefs,
        Dictionary<string, int> pops,
        IReadOnlyList<NarrativeEvent> events)
    {
        Seed = seed;
        Rng = new Rng(seed);
        InitialTribes = initialTribes;
        StartYearBP = startYearBP;
        SeasonsElapsed = seasonsElapsed;
        TribeProductivity = tribeProductivity;
        _chiefs = new Dictionary<string, Chief>(chiefs, System.StringComparer.Ordinal);
        _pops = new Dictionary<string, int>(pops, System.StringComparer.Ordinal);
        _allEvents = new List<NarrativeEvent>(events);
    }

    /// <summary>Advance one season. Succession + pop dynamics run on Spring transition only.</summary>
    public void AdvanceSeason()
    {
        SeasonsElapsed++;
        if (SeasonsElapsed % 4 == 0)
        {
            var events = SuccessionSystem.ProcessYear(_chiefs, CurrentYearBP, Rng);
            PopulationSystem.ProcessYear(_pops, TribeProductivity, _chiefs.Keys, CurrentYearBP, Rng);
            LatestEvents = events;
            _allEvents.AddRange(events);
        }
        else
        {
            LatestEvents = System.Array.Empty<NarrativeEvent>();
        }
    }

    public void AdvanceYear()
    {
        for (var i = 0; i < 4; i++) AdvanceSeason();
    }

    private static Dictionary<string, double> FallbackProductivity(TribeRegistry tribes)
    {
        var d = new Dictionary<string, double>(System.StringComparer.Ordinal);
        foreach (var t in tribes.All) d[t.Id] = 0.5;
        return d;
    }
}
