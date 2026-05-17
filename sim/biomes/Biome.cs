namespace EpochsOfHumanity.Sim.Biomes;

/// <summary>
/// A biome — the environmental archetype of a hex.
/// </summary>
/// <remarks>
/// Loaded from <c>data/biomes/*.json</c>. Pure data, no behaviour.
/// See <c>game-modding</c> skill for the schema.
/// </remarks>
public sealed record Biome(
    string Id,
    string NameKey,                         // localization key, e.g. "biome.carmel_foothills.name"
    string BaseColor,                       // palette key, e.g. "moss-green"
    string[] PatternColors,                 // 1-3 secondary palette keys for tile texture
    BiomePictogram[] Pictograms,            // overlay icons (trees, rocks) with weights
    double Habitability,                    // 0..1 — how comfortable for human settlement
    double HuntingDensity,                  // 0..1 — game animal abundance
    double Productivity)                    // 0..1 — base food production (foraging)
{
    /// <summary>Validates invariants. Throws if config is malformed.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
            throw new System.ArgumentException("Biome.Id required");
        if (string.IsNullOrWhiteSpace(NameKey))
            throw new System.ArgumentException($"Biome '{Id}': NameKey required");
        if (string.IsNullOrWhiteSpace(BaseColor))
            throw new System.ArgumentException($"Biome '{Id}': BaseColor required");
        if (PatternColors.Length == 0)
            throw new System.ArgumentException($"Biome '{Id}': at least one PatternColor required");
        if (Habitability is < 0.0 or > 1.0)
            throw new System.ArgumentException($"Biome '{Id}': Habitability must be in [0,1]");
        if (HuntingDensity is < 0.0 or > 1.0)
            throw new System.ArgumentException($"Biome '{Id}': HuntingDensity must be in [0,1]");
        if (Productivity is < 0.0 or > 1.0)
            throw new System.ArgumentException($"Biome '{Id}': Productivity must be in [0,1]");
    }
}

/// <summary>Pictogram overlay for a biome (e.g. tree, rock, cave entrance).</summary>
public sealed record BiomePictogram(
    string Key,             // sprite key, e.g. "tree-conifer"
    double Weight);         // 0..1 — chance per hex
