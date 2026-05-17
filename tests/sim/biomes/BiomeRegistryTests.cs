using EpochsOfHumanity.Sim.Biomes;
using Xunit;

namespace EpochsOfHumanity.Tests.Sim.Biomes;

public class BiomeRegistryTests
{
    private static Biome MakeValidBiome(string id) => new(
        Id: id,
        NameKey: $"biome.{id.Replace('-', '_')}.name",
        BaseColor: "moss-green",
        PatternColors: new[] { "spring-green" },
        Pictograms: System.Array.Empty<BiomePictogram>(),
        Habitability: 0.5,
        HuntingDensity: 0.5,
        Productivity: 0.5);

    [Fact]
    public void Registry_ExposesBiomes_InOrdinalIdOrder()
    {
        var registry = new BiomeRegistry(new[]
        {
            MakeValidBiome("zagros-foothills"),
            MakeValidBiome("carmel-foothills"),
            MakeValidBiome("levantine-coast"),
        });

        Assert.Equal(3, registry.Count);
        Assert.Equal("carmel-foothills", registry.All[0].Id);
        Assert.Equal("levantine-coast", registry.All[1].Id);
        Assert.Equal("zagros-foothills", registry.All[2].Id);
    }

    [Fact]
    public void Get_KnownId_ReturnsBiome()
    {
        var registry = new BiomeRegistry(new[] { MakeValidBiome("test-biome") });
        var biome = registry.Get("test-biome");
        Assert.Equal("test-biome", biome.Id);
    }

    [Fact]
    public void Get_UnknownId_Throws()
    {
        var registry = new BiomeRegistry(new[] { MakeValidBiome("test") });
        Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
            () => registry.Get("missing"));
    }

    [Fact]
    public void Construction_DuplicateId_Throws()
    {
        Assert.Throws<System.ArgumentException>(() =>
            new BiomeRegistry(new[]
            {
                MakeValidBiome("dup"),
                MakeValidBiome("dup"),
            }));
    }

    [Fact]
    public void Biome_Validate_FailsForInvalidProductivity()
    {
        var bad = MakeValidBiome("bad") with { Productivity = 1.5 };
        Assert.Throws<System.ArgumentException>(() => bad.Validate());
    }

    [Fact]
    public void Biome_Validate_FailsForEmptyPatternColors()
    {
        var bad = MakeValidBiome("bad") with { PatternColors = System.Array.Empty<string>() };
        Assert.Throws<System.ArgumentException>(() => bad.Validate());
    }
}
