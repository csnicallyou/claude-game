using EpochsOfHumanity.Core.Geography;

namespace EpochsOfHumanity.Sim.Characters;

/// <summary>
/// Initial tribe placement for the Levant region at 45,000 BCE.
/// </summary>
/// <remarks>
/// Mix of Sapiens (~4) and Neanderthal (~2) groups. Names use Latin-transliterated
/// archaeological references where appropriate (see historical-research-paleolithic).
///
/// Player starts as <c>Sons of Carmel</c>. Other tribes are static NPCs in v0.1 —
/// gain behaviour in v0.2.
/// </remarks>
public static class LevantTribesPreset
{
    public static TribeRegistry Build()
    {
        return new TribeRegistry(new[]
        {
            // --- Sapiens groups (4) ---
            new Tribe(
                Id: "sons-of-carmel",
                Name: "Sons of Carmel",
                Species: Species.Sapiens,
                HomeHex: new HexCoord(-3, -1),
                IsPlayerControlled: true),

            new Tribe(
                Id: "children-of-hula",
                Name: "Children of Hula",
                Species: Species.Sapiens,
                HomeHex: new HexCoord(-1, -3)),

            new Tribe(
                Id: "folk-of-bekaa",
                Name: "Folk of Bekaa",
                Species: Species.Sapiens,
                HomeHex: new HexCoord(1, -6)),

            new Tribe(
                Id: "hunters-of-negev",
                Name: "Hunters of Negev",
                Species: Species.Sapiens,
                HomeHex: new HexCoord(-1, 4)),

            // --- Neanderthal groups (2) — last surviving in the Levant ---
            new Tribe(
                Id: "neandertal-of-kebara",
                Name: "Neandertal of Kebara",
                Species: Species.Neanderthal,
                HomeHex: new HexCoord(-4, 1)),

            new Tribe(
                Id: "neandertal-of-amud",
                Name: "Neandertal of Amud",
                Species: Species.Neanderthal,
                HomeHex: new HexCoord(0, -5)),
        });
    }
}
