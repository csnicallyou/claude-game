using EpochsOfHumanity.Core.Save;
using EpochsOfHumanity.Sim.Characters;
using EpochsOfHumanity.Sim.State;
using Xunit;

namespace EpochsOfHumanity.Tests.Sim.State;

public class GameStateTests
{
    [Fact]
    public void FreshState_StartsAt45000BP_YearsElapsedZero()
    {
        var state = new GameState("test-seed", LevantTribesPreset.Build());
        Assert.Equal(45_000, state.CurrentYearBP);
        Assert.Equal(0, state.YearsElapsed);
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
    public void RoundTrip_PreservesState()
    {
        var original = new GameState("roundtrip-seed", LevantTribesPreset.Build());
        for (var i = 0; i < 25; i++) original.AdvanceYear();

        var snapshot = SaveStore.ToSnapshot(original, "test-save");
        var bytes = SaveSerializer.Serialize(snapshot);
        var restored = SaveSerializer.Deserialize(bytes);
        var rebuilt = SaveStore.FromSnapshot(restored, LevantTribesPreset.Build());

        Assert.Equal(original.YearsElapsed, rebuilt.YearsElapsed);
        Assert.Equal(original.CurrentYearBP, rebuilt.CurrentYearBP);
        foreach (var (id, chief) in original.Chiefs)
        {
            var rebuiltChief = rebuilt.ChiefOf(id);
            Assert.Equal(chief.Name, rebuiltChief.Name);
            Assert.Equal(chief.AgeWinters, rebuiltChief.AgeWinters);
            Assert.Equal(chief.Sex, rebuiltChief.Sex);
        }
        Assert.Equal(original.AllEvents.Count, rebuilt.AllEvents.Count);
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
        // gzip magic: 0x1f, 0x8b
        Assert.Equal(0x1f, bytes[0]);
        Assert.Equal(0x8b, bytes[1]);
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
