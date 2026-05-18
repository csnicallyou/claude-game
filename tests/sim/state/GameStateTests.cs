using EpochsOfHumanity.Core.Save;
using EpochsOfHumanity.Core.Time;
using EpochsOfHumanity.Sim.Characters;
using EpochsOfHumanity.Sim.State;
using Xunit;

namespace EpochsOfHumanity.Tests.Sim.State;

public class GameStateTests
{
    [Fact]
    public void FreshState_StartsAt45000BP_YearsElapsedZero_Spring()
    {
        var state = new GameState("test-seed", LevantTribesPreset.Build());
        Assert.Equal(45_000, state.CurrentYearBP);
        Assert.Equal(0, state.YearsElapsed);
        Assert.Equal(0L, state.SeasonsElapsed);
        Assert.Equal(Season.Spring, state.CurrentSeason);
    }

    [Fact]
    public void AdvanceSeason_CyclesThroughFourSeasons()
    {
        var state = new GameState("seasons-seed", LevantTribesPreset.Build());

        state.AdvanceSeason();
        Assert.Equal(Season.Summer, state.CurrentSeason);
        Assert.Equal(0, state.YearsElapsed);
        Assert.Equal(45_000, state.CurrentYearBP);

        state.AdvanceSeason();
        Assert.Equal(Season.Autumn, state.CurrentSeason);

        state.AdvanceSeason();
        Assert.Equal(Season.Winter, state.CurrentSeason);
        Assert.Equal(0, state.YearsElapsed);

        // Winter → Spring of next year
        state.AdvanceSeason();
        Assert.Equal(Season.Spring, state.CurrentSeason);
        Assert.Equal(1, state.YearsElapsed);
        Assert.Equal(44_999, state.CurrentYearBP);
    }

    [Fact]
    public void Succession_Runs_OnlyOnSpringTransition()
    {
        var state = new GameState("succession-spring-seed", LevantTribesPreset.Build());
        var origChief = state.ChiefOf("sons-of-carmel");

        // Three sub-year seasons should not change chief age
        state.AdvanceSeason(); // Summer
        Assert.Equal(origChief.AgeWinters, state.ChiefOf("sons-of-carmel").AgeWinters);
        Assert.Empty(state.LatestEvents);

        state.AdvanceSeason(); // Autumn
        Assert.Equal(origChief.AgeWinters, state.ChiefOf("sons-of-carmel").AgeWinters);
        Assert.Empty(state.LatestEvents);

        state.AdvanceSeason(); // Winter
        Assert.Equal(origChief.AgeWinters, state.ChiefOf("sons-of-carmel").AgeWinters);
        Assert.Empty(state.LatestEvents);

        // Spring → succession runs
        state.AdvanceSeason();
        var newChief = state.ChiefOf("sons-of-carmel");
        if (newChief.Name == origChief.Name)
        {
            Assert.Equal(origChief.AgeWinters + 1, newChief.AgeWinters);
        }
        // else: heir replaced
    }

    [Fact]
    public void AdvanceYear_DecrementsYearBP_AndAgesChiefsOrReplacesThem()
    {
        var state = new GameState("test-seed-aging", LevantTribesPreset.Build());
        var origCarmelChief = state.ChiefOf("sons-of-carmel");
        state.AdvanceYear();
        Assert.Equal(44_999, state.CurrentYearBP);

        var newChief = state.ChiefOf("sons-of-carmel");
        // Chief either aged by 1 OR was replaced (heir generated). Both valid outcomes.
        if (newChief.Name == origCarmelChief.Name)
        {
            Assert.Equal(origCarmelChief.AgeWinters + 1, newChief.AgeWinters);
        }
        else
        {
            // Heir replaced — should be in 14-31 winter range per SuccessionSystem
            Assert.InRange(newChief.AgeWinters, 14, 32);
        }
    }

    [Fact]
    public void AdvanceYear_IsDeterministic_WithSameSeed()
    {
        var a = new GameState("repeat-seed", LevantTribesPreset.Build());
        var b = new GameState("repeat-seed", LevantTribesPreset.Build());
        for (var i = 0; i < 100; i++)
        {
            a.AdvanceYear();
            b.AdvanceYear();
        }
        Assert.Equal(a.YearsElapsed, b.YearsElapsed);
        foreach (var (id, chiefA) in a.Chiefs)
        {
            var chiefB = b.ChiefOf(id);
            Assert.Equal(chiefA.Name, chiefB.Name);
            Assert.Equal(chiefA.AgeWinters, chiefB.AgeWinters);
            Assert.Equal(chiefA.Sex, chiefB.Sex);
        }
    }

    [Fact]
    public void AdvanceYear_OverManyYears_ProducesDeathEvents()
    {
        var state = new GameState("longevity-seed", LevantTribesPreset.Build());
        var deathEvents = 0;
        for (var i = 0; i < 200; i++)
        {
            state.AdvanceYear();
            foreach (var ev in state.LatestEvents)
            {
                if (ev.Kind == NarrativeEventKind.ChiefDied) deathEvents++;
            }
        }
        // Over 200 years, expect many chief deaths across 6 tribes
        Assert.True(deathEvents > 5, $"Expected multiple chief deaths over 200 years, got {deathEvents}");
    }

    [Fact]
    public void DeathProbability_IncreasesMonotonically_WithAge()
    {
        var p20 = SuccessionSystem.DeathProbability(20);
        var p40 = SuccessionSystem.DeathProbability(40);
        var p60 = SuccessionSystem.DeathProbability(60);
        var p70 = SuccessionSystem.DeathProbability(70);
        Assert.True(p20 < p40, $"20→40 should rise: {p20} → {p40}");
        Assert.True(p40 < p60, $"40→60 should rise: {p40} → {p60}");
        Assert.True(p60 < p70, $"60→70 should rise: {p60} → {p70}");
    }
}

public class SaveStoreTests
{
    [Fact]
    public void RoundTrip_PreservesState_AcrossSeasons()
    {
        var initial = LevantTribesPreset.Build();
        var prod = MakeUniformProductivity(initial, 0.5);

        var original = new GameState("roundtrip-seed", initial, prod);
        for (var i = 0; i < 25; i++) original.AdvanceYear();
        original.AdvanceSeason();

        var snapshot = SaveStore.ToSnapshot(original, "test-save");
        var bytes = SaveSerializer.Serialize(snapshot);
        var restored = SaveSerializer.Deserialize(bytes);

        var freshTribes = LevantTribesPreset.Build();
        var freshProd = MakeUniformProductivity(freshTribes, 0.5);
        var rebuilt = SaveStore.FromSnapshot(restored, freshTribes, freshProd);

        Assert.Equal(original.SeasonsElapsed, rebuilt.SeasonsElapsed);
        Assert.Equal(original.YearsElapsed, rebuilt.YearsElapsed);
        Assert.Equal(original.CurrentYearBP, rebuilt.CurrentYearBP);
        Assert.Equal(original.CurrentSeason, rebuilt.CurrentSeason);
        foreach (var (id, chief) in original.Chiefs)
        {
            var rebuiltChief = rebuilt.ChiefOf(id);
            Assert.Equal(chief.Name, rebuiltChief.Name);
            Assert.Equal(chief.AgeWinters, rebuiltChief.AgeWinters);
            Assert.Equal(chief.Sex, rebuiltChief.Sex);
        }
        foreach (var (id, pop) in original.Pops)
        {
            Assert.Equal(pop, rebuilt.PopOf(id));
        }
        Assert.Equal(original.AllEvents.Count, rebuilt.AllEvents.Count);
    }

    private static System.Collections.Generic.Dictionary<string, double> MakeUniformProductivity(
        TribeRegistry tribes, double value)
    {
        var d = new System.Collections.Generic.Dictionary<string, double>(System.StringComparer.Ordinal);
        foreach (var t in tribes.All) d[t.Id] = value;
        return d;
    }

    [Fact]
    public void Serialize_ProducesNonEmpty_Compressed()
    {
        var state = new GameState("compress-seed", LevantTribesPreset.Build());
        for (var i = 0; i < 10; i++) state.AdvanceYear();
        var snapshot = SaveStore.ToSnapshot(state, "test");
        var bytes = SaveSerializer.Serialize(snapshot);
        Assert.True(bytes.Length > 100);
        Assert.True(bytes.Length < 50_000, $"Save too large: {bytes.Length} bytes");
        Assert.Equal(0x1f, bytes[0]);
        Assert.Equal(0x8b, bytes[1]);
    }
}

public class PopulationSystemTests
{
    [Fact]
    public void FreshState_HasInitialPops_AllAboveFloor()
    {
        var state = new GameState("pops-init-seed", LevantTribesPreset.Build());
        foreach (var (_, pop) in state.Pops)
        {
            Assert.True(pop >= EpochsOfHumanity.Sim.Pops.PopulationSystem.FloorPop,
                $"Initial pop {pop} below floor");
        }
    }

    [Fact]
    public void InitialPop_SapiensLargerThan_Neanderthal_OnAverage()
    {
        // Run with many seeds and average — Sapiens band sizes are larger by design.
        var sapTotal = 0;
        var neaTotal = 0;
        for (var s = 0; s < 40; s++)
        {
            var state = new GameState($"avg-{s}", LevantTribesPreset.Build());
            sapTotal += state.PopOf("sons-of-carmel");
            neaTotal += state.PopOf("neandertal-of-kebara");
        }
        var sapAvg = sapTotal / 40.0;
        var neaAvg = neaTotal / 40.0;
        Assert.True(sapAvg > neaAvg,
            $"Sapiens band ({sapAvg:F1}) should average larger than Neanderthal ({neaAvg:F1})");
    }

    [Fact]
    public void AdvanceYear_ChangesPops_Deterministically()
    {
        var prod = new System.Collections.Generic.Dictionary<string, double>(System.StringComparer.Ordinal)
        {
            ["sons-of-carmel"] = 0.7, ["children-of-hula"] = 0.85, ["folk-of-bekaa"] = 0.55,
            ["hunters-of-negev"] = 0.30, ["neandertal-of-kebara"] = 0.7, ["neandertal-of-amud"] = 0.85,
        };

        var a = new GameState("popseed", LevantTribesPreset.Build(), prod);
        var b = new GameState("popseed", LevantTribesPreset.Build(), prod);
        for (var i = 0; i < 50; i++) { a.AdvanceYear(); b.AdvanceYear(); }

        foreach (var (id, popA) in a.Pops)
        {
            Assert.Equal(popA, b.PopOf(id));
        }
    }

    [Fact]
    public void HighProductivity_GrowsPop_LowProductivity_ShrinksOrStable()
    {
        var lowProd = new System.Collections.Generic.Dictionary<string, double>(System.StringComparer.Ordinal);
        var highProd = new System.Collections.Generic.Dictionary<string, double>(System.StringComparer.Ordinal);
        foreach (var t in LevantTribesPreset.Build().All)
        {
            lowProd[t.Id] = 0.15;
            highProd[t.Id] = 0.85;
        }

        var low = new GameState("low", LevantTribesPreset.Build(), lowProd);
        var high = new GameState("high", LevantTribesPreset.Build(), highProd);
        for (var i = 0; i < 80; i++) { low.AdvanceYear(); high.AdvanceYear(); }

        // After many years, high-productivity tribes should be markedly larger.
        var lowTotal = 0;
        var highTotal = 0;
        foreach (var (_, p) in low.Pops) lowTotal += p;
        foreach (var (_, p) in high.Pops) highTotal += p;

        Assert.True(highTotal > lowTotal * 2,
            $"High-prod total ({highTotal}) should be ≥ 2× low-prod ({lowTotal})");
    }

    [Fact]
    public void PopFloor_HoldsAtFiveSouls()
    {
        // Tiny productivity — pops should crash but not below floor.
        var zeroProd = new System.Collections.Generic.Dictionary<string, double>(System.StringComparer.Ordinal);
        foreach (var t in LevantTribesPreset.Build().All) zeroProd[t.Id] = 0.01;

        var state = new GameState("crash", LevantTribesPreset.Build(), zeroProd);
        for (var i = 0; i < 200; i++) state.AdvanceYear();

        foreach (var (_, p) in state.Pops)
        {
            Assert.True(p >= EpochsOfHumanity.Sim.Pops.PopulationSystem.FloorPop);
        }
    }
}

public class NameGeneratorTests
{
    [Fact]
    public void Generate_IsDeterministic_SameRngState()
    {
        var a = new EpochsOfHumanity.Core.Prng.Rng("name-seed");
        var b = new EpochsOfHumanity.Core.Prng.Rng("name-seed");
        for (var i = 0; i < 50; i++)
        {
            var na = NameGenerator.Generate(a, Sex.Male);
            var nb = NameGenerator.Generate(b, Sex.Male);
            Assert.Equal(na, nb);
        }
    }

    [Fact]
    public void Generate_ProducesNonEmptyCapitalized_Names()
    {
        var rng = new EpochsOfHumanity.Core.Prng.Rng("nonempty-seed");
        for (var i = 0; i < 100; i++)
        {
            var n = NameGenerator.Generate(rng, i % 2 == 0 ? Sex.Male : Sex.Female);
            Assert.False(string.IsNullOrEmpty(n));
            Assert.True(char.IsUpper(n[0]), $"Name '{n}' should start uppercase");
            Assert.True(n.Length >= 4, $"Name '{n}' too short");
        }
    }
}
