using EpochsOfHumanity.Core.Geography;

namespace EpochsOfHumanity.Sim.Geography;

/// <summary>
/// One hex on the strategic map. Pure data, no behaviour.
/// </summary>
/// <remarks>
/// In Arch ECS (when wired), this will be split into components.
/// For v0.1 we use a flat record for simplicity — refactor to ECS in v0.2 when
/// the first per-tick system needs it.
/// </remarks>
public sealed record HexTile(
    HexCoord Coord,
    string BiomeId);
