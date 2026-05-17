using EpochsOfHumanity.Core.Geography;
using EpochsOfHumanity.Sim.Geography;
using Xunit;

namespace EpochsOfHumanity.Tests.Sim.Geography;

public class HexMapTests
{
    [Fact]
    public void EmptyMap_HasZeroCount()
    {
        var map = new HexMap();
        Assert.Equal(0, map.Count);
        Assert.Empty(map.AllOrdered());
    }

    [Fact]
    public void Add_StoresTile_RetrievableByCoord()
    {
        var map = new HexMap();
        var coord = new HexCoord(2, -1);
        var tile = new HexTile(coord, "carmel-foothills");
        map.Add(tile);

        Assert.Equal(1, map.Count);
        Assert.True(map.Contains(coord));
        Assert.Equal(tile, map.Get(coord));
    }

    [Fact]
    public void Add_Duplicate_Throws()
    {
        var map = new HexMap();
        var coord = new HexCoord(0, 0);
        map.Add(new HexTile(coord, "carmel-foothills"));
        Assert.Throws<System.ArgumentException>(
            () => map.Add(new HexTile(coord, "negev-arid")));
    }

    [Fact]
    public void Get_Missing_Throws()
    {
        var map = new HexMap();
        Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
            () => map.Get(new HexCoord(0, 0)));
    }

    [Fact]
    public void AllOrdered_ReturnsTiles_InQThenR_Order()
    {
        var map = new HexMap();
        map.Add(new HexTile(new HexCoord(2, 0), "a"));
        map.Add(new HexTile(new HexCoord(0, 0), "b"));
        map.Add(new HexTile(new HexCoord(1, 2), "c"));
        map.Add(new HexTile(new HexCoord(1, -1), "d"));

        var ordered = new System.Collections.Generic.List<HexTile>(map.AllOrdered());
        Assert.Equal(new HexCoord(0, 0), ordered[0].Coord);
        Assert.Equal(new HexCoord(1, -1), ordered[1].Coord);
        Assert.Equal(new HexCoord(1, 2), ordered[2].Coord);
        Assert.Equal(new HexCoord(2, 0), ordered[3].Coord);
    }

    [Fact]
    public void Bounds_ComputedCorrectly()
    {
        var map = new HexMap();
        map.Add(new HexTile(new HexCoord(-3, 5), "a"));
        map.Add(new HexTile(new HexCoord(7, -2), "b"));
        map.Add(new HexTile(new HexCoord(1, 1), "c"));

        var (minQ, maxQ, minR, maxR) = map.Bounds();
        Assert.Equal(-3, minQ);
        Assert.Equal(7, maxQ);
        Assert.Equal(-2, minR);
        Assert.Equal(5, maxR);
    }
}

public class LevantPresetTests
{
    [Fact]
    public void Build_ProducesReasonableHexCount()
    {
        var map = LevantPreset.Build();
        // We expect ~150-300 hexes — irregular Levant shape after geometry filtering
        Assert.InRange(map.Count, 100, 400);
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        var a = LevantPreset.Build();
        var b = LevantPreset.Build();
        Assert.Equal(a.Count, b.Count);

        var listA = new System.Collections.Generic.List<HexTile>(a.AllOrdered());
        var listB = new System.Collections.Generic.List<HexTile>(b.AllOrdered());
        Assert.Equal(listA.Count, listB.Count);
        for (var i = 0; i < listA.Count; i++)
        {
            Assert.Equal(listA[i].Coord, listB[i].Coord);
            Assert.Equal(listA[i].BiomeId, listB[i].BiomeId);
        }
    }

    [Fact]
    public void Build_ContainsStartingHex()
    {
        var map = LevantPreset.Build();
        Assert.True(map.Contains(LevantPreset.StartingHex),
            $"Starting hex {LevantPreset.StartingHex} must be in the map");

        var startTile = map.Get(LevantPreset.StartingHex);
        // Starting hex should be carmel-foothills (Mount Carmel area)
        Assert.Equal("carmel-foothills", startTile.BiomeId);
    }

    [Fact]
    public void Build_UsesOnlyKnownBiomes()
    {
        var knownBiomes = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
        {
            "levantine-coast", "carmel-foothills", "jordan-valley",
            "sinai-desert", "negev-arid", "lebanon-cedars", "zagros-foothills",
        };

        var map = LevantPreset.Build();
        foreach (var tile in map.AllOrdered())
        {
            Assert.Contains(tile.BiomeId, knownBiomes);
        }
    }

    [Fact]
    public void Build_HasCoastInWest_DesertInSouth()
    {
        var map = LevantPreset.Build();
        var coastTiles = 0;
        var desertTiles = 0;
        foreach (var tile in map.AllOrdered())
        {
            if (tile.BiomeId == "levantine-coast") coastTiles++;
            if (tile.BiomeId == "sinai-desert") desertTiles++;
        }
        Assert.True(coastTiles > 5, $"Expected coast tiles in western Levant, got {coastTiles}");
        Assert.True(desertTiles > 3, $"Expected Sinai desert tiles in south, got {desertTiles}");
    }
}
