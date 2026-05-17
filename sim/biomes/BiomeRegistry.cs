using System.Collections.Generic;

namespace EpochsOfHumanity.Sim.Biomes;

/// <summary>
/// Read-only lookup of all loaded biomes.
/// </summary>
/// <remarks>
/// Built once at world init from <c>data/biomes/*.json</c>. Biomes sorted by Id
/// for deterministic iteration (Law 1).
/// </remarks>
public sealed class BiomeRegistry
{
    private readonly Dictionary<string, Biome> _byId;
    private readonly Biome[] _ordered;

    public BiomeRegistry(IEnumerable<Biome> biomes)
    {
        _byId = new Dictionary<string, Biome>(System.StringComparer.Ordinal);
        var temp = new List<Biome>();
        foreach (var b in biomes)
        {
            b.Validate();
            if (!_byId.TryAdd(b.Id, b))
                throw new System.ArgumentException($"Duplicate biome id: '{b.Id}'");
            temp.Add(b);
        }
        temp.Sort((a, b) => System.StringComparer.Ordinal.Compare(a.Id, b.Id));
        _ordered = temp.ToArray();
    }

    public int Count => _ordered.Length;

    public Biome Get(string id)
        => _byId.TryGetValue(id, out var b)
            ? b
            : throw new KeyNotFoundException($"Biome '{id}' not registered");

    public bool TryGet(string id, out Biome biome)
        => _byId.TryGetValue(id, out biome!);

    /// <summary>All biomes in deterministic order (sorted by Id, Ordinal).</summary>
    public IReadOnlyList<Biome> All => _ordered;
}
