namespace EpochsOfHumanity.Core.Geography;

/// <summary>
/// Axial coordinates for a pointy-top hexagonal grid.
/// </summary>
/// <remarks>
/// Standard axial system (q, r) per Red Blob Games convention. We use pointy-top
/// orientation (see game-visual-style skill, §"Гекс-тайлы"). Cube coordinate s
/// is derived: s = -q - r.
///
/// Neighbours order is fixed (E, NE, NW, W, SW, SE) — deterministic iteration,
/// see Law 1 (game-determinism).
/// </remarks>
public readonly record struct HexCoord(int Q, int R)
{
    /// <summary>Derived cube coordinate. Always q + r + s == 0.</summary>
    public int S => -Q - R;

    /// <summary>
    /// Six neighbour directions in fixed order: E, NE, NW, W, SW, SE.
    /// Used for deterministic iteration over neighbours.
    /// </summary>
    public static readonly HexCoord[] Directions =
    {
        new( 1,  0),  // E
        new( 1, -1),  // NE
        new( 0, -1),  // NW
        new(-1,  0),  // W
        new(-1,  1),  // SW
        new( 0,  1),  // SE
    };

    public HexCoord Neighbour(HexDirection dir)
    {
        var d = Directions[(int)dir];
        return new HexCoord(Q + d.Q, R + d.R);
    }

    /// <summary>Six neighbours in fixed direction order.</summary>
    public System.Collections.Generic.IEnumerable<HexCoord> Neighbours()
    {
        foreach (var d in Directions)
            yield return new HexCoord(Q + d.Q, R + d.R);
    }

    /// <summary>
    /// Hex distance (number of steps along the grid). Equivalent to cube distance.
    /// </summary>
    public int DistanceTo(HexCoord other)
    {
        var dq = Q - other.Q;
        var dr = R - other.R;
        var ds = S - other.S;
        return (System.Math.Abs(dq) + System.Math.Abs(dr) + System.Math.Abs(ds)) / 2;
    }

    public static HexCoord operator +(HexCoord a, HexCoord b) => new(a.Q + b.Q, a.R + b.R);
    public static HexCoord operator -(HexCoord a, HexCoord b) => new(a.Q - b.Q, a.R - b.R);

    public override string ToString() => $"({Q},{R})";
}

/// <summary>Direction labels matching <see cref="HexCoord.Directions"/> order.</summary>
public enum HexDirection
{
    East = 0,
    NorthEast = 1,
    NorthWest = 2,
    West = 3,
    SouthWest = 4,
    SouthEast = 5,
}
