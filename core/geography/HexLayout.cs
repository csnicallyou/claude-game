using System.Numerics;

namespace EpochsOfHumanity.Core.Geography;

/// <summary>
/// Converts between hex (axial) and pixel coordinates for pointy-top hexagons.
/// </summary>
/// <remarks>
/// Pure math, engine-agnostic. Uses <see cref="Vector2"/> from System.Numerics
/// (NOT Godot.Vector2 — Law 2). The render layer converts to Godot.Vector2 at
/// the boundary.
///
/// "Size" here = the radius from hex centre to corner (= length of one side).
/// For our 64×64 px hex tile: size ≈ 18.5 px (because hex bounding box is 2*size wide
/// and √3*size tall in pointy-top).
/// </remarks>
public readonly record struct HexLayout(double Size, Vector2 Origin)
{
    private static readonly double Sqrt3 = System.Math.Sqrt(3.0);

    public static HexLayout Default => new(Size: 18.475, Origin: Vector2.Zero);

    /// <summary>Convert axial hex coordinate to pixel position of hex centre.</summary>
    public Vector2 HexToPixel(HexCoord hex)
    {
        var x = Size * Sqrt3 * (hex.Q + hex.R / 2.0);
        var y = Size * 1.5 * hex.R;
        return new Vector2(Origin.X + (float)x, Origin.Y + (float)y);
    }

    /// <summary>
    /// Convert pixel position back to fractional axial coords, then round to nearest hex.
    /// Used for mouse-pick.
    /// </summary>
    public HexCoord PixelToHex(Vector2 px)
    {
        var x = (px.X - Origin.X) / Size;
        var y = (px.Y - Origin.Y) / Size;

        var q = (Sqrt3 / 3.0 * x) - (1.0 / 3.0 * y);
        var r = (2.0 / 3.0) * y;
        return RoundFractional(q, r);
    }

    private static HexCoord RoundFractional(double q, double r)
    {
        var s = -q - r;
        var rq = System.Math.Round(q);
        var rr = System.Math.Round(r);
        var rs = System.Math.Round(s);

        var qDiff = System.Math.Abs(rq - q);
        var rDiff = System.Math.Abs(rr - r);
        var sDiff = System.Math.Abs(rs - s);

        if (qDiff > rDiff && qDiff > sDiff)
            rq = -rr - rs;
        else if (rDiff > sDiff)
            rr = -rq - rs;
        // else rs = -rq - rr (implied)

        return new HexCoord((int)rq, (int)rr);
    }

    /// <summary>The 6 corner offsets from a hex centre, in pixel coordinates.</summary>
    public Vector2[] Corners()
    {
        var corners = new Vector2[6];
        for (var i = 0; i < 6; i++)
        {
            // Pointy-top: first corner is at 30°, then every 60°
            var angle = System.Math.PI / 180.0 * (60.0 * i - 30.0);
            corners[i] = new Vector2(
                (float)(Size * System.Math.Cos(angle)),
                (float)(Size * System.Math.Sin(angle)));
        }
        return corners;
    }
}
