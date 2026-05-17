using EpochsOfHumanity.Core.Geography;

namespace EpochsOfHumanity.Sim.Geography;

/// <summary>
/// Builds the Levant region map for v0.1 — ~250 hexes roughly matching the real
/// geography of the Levant at 45,000 BCE.
/// </summary>
/// <remarks>
/// Scale: 1 hex ≈ 40-50 km. North-South extent: ~1000 km (Lebanon → Sinai),
/// East-West: ~500 km (Mediterranean → Zagros).
///
/// Pointy-top axial coordinates:
///   +q = roughly east, -q = roughly west,
///   +r = roughly south, -r = roughly north.
/// Axes aren't perfectly orthogonal in pointy-top but close enough for region masks.
///
/// Biome assignment is geographical (zones by axial position), not random —
/// deterministic, same map every time. See <c>historical-research-paleolithic</c>
/// for biome reference; <c>game-visual-style</c> for palette mapping.
/// </remarks>
public static class LevantPreset
{
    private const int MinQ = -8;
    private const int MaxQ = 8;
    private const int MinR = -10;
    private const int MaxR = 10;

    /// <summary>Build the map (~200 tiles after geometry filtering).</summary>
    public static HexMap Build()
    {
        var map = new HexMap();

        for (var q = MinQ; q <= MaxQ; q++)
        {
            for (var r = MinR; r <= MaxR; r++)
            {
                if (!IsInLevant(q, r)) continue;

                var biomeId = AssignBiome(q, r);
                map.Add(new HexTile(new HexCoord(q, r), biomeId));
            }
        }

        return map;
    }

    /// <summary>
    /// Geometric mask in axial coordinates. Shape approximates the real Levant:
    /// Mediterranean cut-off on west (irregular coast — bays and headlands),
    /// Zagros bound on east, narrow corridor in the middle (Carmel-Galilee-Bekaa),
    /// wider in the south (Sinai-Negev triangle).
    /// </summary>
    private static bool IsInLevant(int q, int r)
    {
        // West cut — base coastline curves slightly east as we go south,
        // then perturbed deterministically per (q,r) for natural-looking bays/headlands.
        var baseWest = -7 + (r > 4 ? 1 : 0) + (r > 7 ? 1 : 0);
        var perturbation = CoastPerturbation(q, r);
        if (q < baseWest + perturbation) return false;

        // East cut (Zagros foothills = our east border).
        // North-east (Anti-Taurus, Lebanon mountains): tighter east-west extent.
        var eastLimit = 7;
        if (r < -5) eastLimit = 5;
        if (q > eastLimit) return false;

        // North cut
        if (r < -10) return false;
        // South cut
        if (r > 10) return false;
        // Far south-east: avoid Arabia
        if (r > 6 && q > 3) return false;

        return true;
    }

    /// <summary>
    /// Deterministic coastline perturbation. Returns:
    ///   -1 → coastline pulled west (this hex becomes a headland into the sea),
    ///    0 → no change,
    ///   +1 → coastline pushed east (this hex becomes a bay/cut-out).
    /// </summary>
    /// <remarks>
    /// Only relevant for hexes near the western boundary; inland hexes are
    /// unaffected because their q is well east of the limit either way.
    /// Pure hash-based — no PRNG state, so same map every build.
    /// </remarks>
    private static int CoastPerturbation(int q, int r)
    {
        // Stable per-coord hash. Two primes XOR'd — Bob Jenkins style.
        var h = unchecked((uint)((q + 1024) * 73856093) ^ (uint)((r + 1024) * 19349663));
        var noise = h % 100u;

        // ~22% headlands (q-1 land that would otherwise be sea)
        if (noise < 22) return -1;
        // ~18% bays (q+1 sea that would otherwise be land)
        if (noise < 40) return 1;
        return 0;
    }

    /// <summary>Assign a biome id by axial-coordinate zone.</summary>
    private static string AssignBiome(int q, int r)
    {
        // --- Far north (Lebanon cedars) ---
        if (r <= -6) return "lebanon-cedars";

        // --- Far east (Zagros foothills) ---
        if (q >= 5) return "zagros-foothills";

        // --- Far south (Sinai / desert) ---
        if (r >= 7) return "sinai-desert";

        // --- Negev arid zone (south-center, between Carmel and Sinai) ---
        if (r >= 4 && q <= 1) return "negev-arid";

        // --- Mediterranean coast (narrow strip along west edge) ---
        // Coast band: 1-2 hexes from the base western boundary.
        // Headland hexes (q below base) also count as coast.
        var baseWest = -7 + (r > 4 ? 1 : 0) + (r > 7 ? 1 : 0);
        if (q - baseWest <= 1) return "levantine-coast";

        // --- Jordan valley (narrow N-S corridor) ---
        // The Jordan rift roughly runs at q ≈ -2 to 0 in our axial system.
        if ((q == -2 || q == -1) && r >= -4 && r <= 4) return "jordan-valley";

        // --- Default: Carmel-style foothills (central highlands) ---
        return "carmel-foothills";
    }

    /// <summary>
    /// Recommended starting hex for v0.1 — Mount Carmel area
    /// (real archaeological cluster: Skhul, Qafzeh, Kebara, Tabun caves).
    /// </summary>
    public static HexCoord StartingHex => new(-3, -1);
}
