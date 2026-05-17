using EpochsOfHumanity.Core.Geography;

namespace EpochsOfHumanity.Sim.Characters;

/// <summary>
/// A tribe — a coherent social group living at a home hex on the strategic map.
/// </summary>
/// <remarks>
/// Pure data, no behaviour. In v0.1 tribes are static markers; from v0.2 they
/// gain population (Pop component), AI, migration, diplomacy etc.
///
/// <c>Name</c> uses Latin-transliterated archaeological/place-based naming per
/// CLAUDE.md §7 — not translated across locales.
/// </remarks>
public sealed record Tribe(
    string Id,
    string Name,             // "Sons of Carmel", "Neandertal of Kebara" — latin, untranslated
    Species Species,
    HexCoord HomeHex,
    bool IsPlayerControlled = false);
