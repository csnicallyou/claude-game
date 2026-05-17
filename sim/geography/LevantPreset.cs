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
    /// Mediterranean cut-off on west, Zagros bound on east, narrow corridor in
    /// the middle (Carmel-Galilee-Bekaa), wider in the south (Sinai-Negev triangle).
    /// </summary>
    private static bool IsInLevant(int q, int r)
    {
        // West cut (Mediterranean sea — coastline curves slightly east as we go south).
        // r negative = north → coast at q = -7; r positive = south → coast at q = -6 to -5.
        var westLimit = -7 + (r > 4 ? 1 : 0) + (r > 7 ? 1 : 0);
        if (q < westLimit) return false;

        // East cut (Zagros foothills = our east border).
        // North-east (Anti-Taurus, Lebanon mountains): tighter east-west extent.
        var eastLimit = 7;
        if (r < -5) eastLimit = 5; // Lebanon ridge narrows region
        if (q > eastLimit) return false;

        // North cut (above Lebanon — we don't model further).
        if (r < -10) return false;

        // South cut (deep desert is outside region).
        if (r > 10) return false;

        // Far south-east: avoid stretching into Arabia.
        if (r > 6 && q > 3) return false;

        return true;
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
        // Coast band: 1-2 hexes inside the western boundary.
        var westLimit = -7 + (r > 4 ? 1 : 0) + (r > 7 ? 1 : 0);
        if (q - westLimit <= 1) return "levantine-coast";

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
