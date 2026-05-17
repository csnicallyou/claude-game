using EpochsOfHumanity.Core.Geography;

namespace EpochsOfHumanity.Sim.Geography;

/// <summary>
/// Real major rivers of the Levant at 45,000 BCE — approximate paths in our axial grid.
/// </summary>
/// <remarks>
/// Names latin-transliterated, untranslated (CLAUDE.md §7).
///
/// Real geography:
/// - <b>Jordan</b>: flows S from Mt. Hermon foothills through Hula valley,
///   Sea of Galilee, down to the Dead Sea basin (then a paleo-lake, larger than today).
/// - <b>Litani</b>: rises in Bekaa valley, flows S then W to Mediterranean (south Lebanon).
/// - <b>Orontes</b>: rises in Bekaa, flows N through Syria to Turkey (out of our region).
/// </remarks>
public static class LevantRivers
{
    public static River[] All() => new[]
    {
        new River(
            Id: "jordan",
            NameKey: "river.jordan.name",
            Path: new HexCoord[]
            {
                new( 1, -5),  // Mt. Hermon foothills (source)
                new( 0, -4),
                new( 0, -3),  // Hula
                new( 0, -2),
                new(-1, -1),  // approaching Galilee
                new(-1,  0),  // Sea of Galilee area
                new(-1,  1),
                new(-1,  2),
                new(-1,  3),  // Jordan valley narrowing
                new(-1,  4),  // Dead Sea basin (mouth, endorheic — no outflow)
            }),

        new River(
            Id: "litani",
            NameKey: "river.litani.name",
            Path: new HexCoord[]
            {
                new( 1, -6),  // Bekaa (source)
                new( 0, -6),
                new(-1, -6),  // turns west
                new(-2, -5),
                new(-3, -5),
                new(-4, -5),
                new(-5, -4),  // mouth at Mediterranean (south Lebanon coast)
            }),

        new River(
            Id: "orontes",
            NameKey: "river.orontes.name",
            Path: new HexCoord[]
            {
                new( 2, -6),  // Bekaa (source — shares headwaters with Litani, real geography)
                new( 2, -7),  // flows N
                new( 2, -8),
                new( 1, -9),
                new( 1, -10), // exits map to N
            }),
    };
}
