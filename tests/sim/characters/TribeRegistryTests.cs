using EpochsOfHumanity.Core.Geography;
using EpochsOfHumanity.Sim.Characters;
using EpochsOfHumanity.Sim.Geography;
using Xunit;

namespace EpochsOfHumanity.Tests.Sim.Characters;

public class TribeRegistryTests
{
    [Fact]
    public void Build_ProducesSixTribes_OnePlayer()
    {
        var registry = LevantTribesPreset.Build();
        Assert.Equal(6, registry.Count);

        var players = new System.Collections.Generic.List<Tribe>();
        foreach (var t in registry.All)
            if (t.IsPlayerControlled) players.Add(t);
        Assert.Single(players);
        Assert.Equal("sons-of-carmel", players[0].Id);
    }

    [Fact]
    public void Build_ContainsBothSpecies()
    {
        var registry = LevantTribesPreset.Build();
        var sapiens = 0;
        var neanderthal = 0;
        foreach (var t in registry.All)
        {
            if (t.Species == Species.Sapiens) sapiens++;
            else if (t.Species == Species.Neanderthal) neanderthal++;
        }
        Assert.True(sapiens >= 3, $"Expected ≥3 sapiens tribes, got {sapiens}");
        Assert.True(neanderthal >= 1, $"Expected ≥1 neanderthal tribe, got {neanderthal}");
    }

    [Fact]
    public void AllTribeHomes_AreInsideLevantMap()
    {
        var registry = LevantTribesPreset.Build();
        var map = LevantPreset.Build();
        foreach (var tribe in registry.All)
        {
            Assert.True(map.Contains(tribe.HomeHex),
                $"Tribe '{tribe.Name}' at {tribe.HomeHex} not in Levant map");
        }
    }

    [Fact]
    public void Duplicate_HomeHex_Throws()
    {
        var dup = new[]
        {
            new Tribe("a", "A", Species.Sapiens, new HexCoord(0, 0)),
            new Tribe("b", "B", Species.Sapiens, new HexCoord(0, 0)),
        };
        Assert.Throws<System.ArgumentException>(() => new TribeRegistry(dup));
    }

    [Fact]
    public void AtHex_KnownHex_ReturnsTribe()
    {
        var registry = LevantTribesPreset.Build();
        var atCarmel = registry.AtHex(new HexCoord(-3, -1));
        Assert.NotNull(atCarmel);
        Assert.Equal("sons-of-carmel", atCarmel.Id);
    }

    [Fact]
    public void AtHex_EmptyHex_ReturnsNull()
    {
        var registry = LevantTribesPreset.Build();
        Assert.Null(registry.AtHex(new HexCoord(100, 100)));
    }
}

public class LevantRiversTests
{
    [Fact]
    public void AllRivers_HaveAtLeastTwoPoints()
    {
        foreach (var river in LevantRivers.All())
        {
            Assert.True(river.Path.Count >= 2,
                $"River '{river.Id}' has only {river.Path.Count} point(s)");
        }
    }

    [Fact]
    public void RiverIds_AreUnique()
    {
        var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach (var r in LevantRivers.All())
        {
            Assert.True(seen.Add(r.Id), $"Duplicate river id: {r.Id}");
        }
    }

    [Fact]
    public void HasJordan_LitaniAnd_Orontes()
    {
        var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach (var r in LevantRivers.All()) ids.Add(r.Id);
        Assert.Contains("jordan", ids);
        Assert.Contains("litani", ids);
        Assert.Contains("orontes", ids);
    }
}
