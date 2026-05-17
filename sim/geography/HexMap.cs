using System.Collections.Generic;
using EpochsOfHumanity.Core.Geography;

namespace EpochsOfHumanity.Sim.Geography;

/// <summary>
/// The strategic map: a collection of <see cref="HexTile"/>s indexed by coordinate.
/// </summary>
/// <remarks>
/// Engine-agnostic. Iteration is deterministic: tiles are returned in (Q, R)
/// lexicographic order. The map is mutable during world build, then becomes effectively
/// frozen for the sim (changes go through Commands).
/// </remarks>
public sealed class HexMap
{
    private readonly Dictionary<HexCoord, HexTile> _tiles = new();

    public int Count => _tiles.Count;

    public void Add(HexTile tile)
    {
        if (!_tiles.TryAdd(tile.Coord, tile))
            throw new System.ArgumentException($"Tile already exists at {tile.Coord}");
    }

    public HexTile Get(HexCoord coord)
        => _tiles.TryGetValue(coord, out var t)
            ? t
            : throw new KeyNotFoundException($"No tile at {coord}");

    public bool TryGet(HexCoord coord, out HexTile tile)
        => _tiles.TryGetValue(coord, out tile!);

    public bool Contains(HexCoord coord) => _tiles.ContainsKey(coord);

    /// <summary>
    /// All tiles in deterministic order: ascending Q, then ascending R.
    /// Always use this for iteration that affects state (Law 1).
    /// </summary>
    public IEnumerable<HexTile> AllOrdered()
    {
        var keys = new List<HexCoord>(_tiles.Keys);
        keys.Sort(static (a, b) =>
        {
            var cq = a.Q.CompareTo(b.Q);
            return cq != 0 ? cq : a.R.CompareTo(b.R);
        });
        foreach (var k in keys)
            yield return _tiles[k];
    }

    /// <summary>Returns the bounding box in axial coords. Useful for camera setup.</summary>
    public (int MinQ, int MaxQ, int MinR, int MaxR) Bounds()
    {
        if (_tiles.Count == 0) return (0, 0, 0, 0);
        int minQ = int.MaxValue, maxQ = int.MinValue;
        int minR = int.MaxValue, maxR = int.MinValue;
        foreach (var c in _tiles.Keys)
        {
            if (c.Q < minQ) minQ = c.Q;
            if (c.Q > maxQ) maxQ = c.Q;
            if (c.R < minR) minR = c.R;
            if (c.R > maxR) maxR = c.R;
        }
        return (minQ, maxQ, minR, maxR);
    }
}
