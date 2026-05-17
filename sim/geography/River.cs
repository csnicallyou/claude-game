using System.Collections.Generic;
using EpochsOfHumanity.Core.Geography;

namespace EpochsOfHumanity.Sim.Geography;

/// <summary>
/// A river: a sequence of hex coordinates from source to mouth.
/// </summary>
/// <remarks>
/// V0.1: rivers are static, defined by <see cref="LevantRivers"/>.
/// Future versions: rivers can dry up (climate events, Pillar 5),
/// shift course over millennia, get crossed by bridges (post-paleolithic).
/// </remarks>
public sealed record River(
    string Id,
    string NameKey,                 // localization key
    IReadOnlyList<HexCoord> Path);  // source [0] → mouth [Count-1]
