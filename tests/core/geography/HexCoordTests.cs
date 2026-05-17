using EpochsOfHumanity.Core.Geography;
using Xunit;

namespace EpochsOfHumanity.Tests.Core.Geography;

public class HexCoordTests
{
    [Fact]
    public void Origin_HasSixNeighbours_AllAtDistanceOne()
    {
        var origin = new HexCoord(0, 0);
        var neighbours = new System.Collections.Generic.List<HexCoord>(origin.Neighbours());

        Assert.Equal(6, neighbours.Count);
        foreach (var n in neighbours)
        {
            Assert.Equal(1, origin.DistanceTo(n));
        }
    }

    [Fact]
    public void CubeCoordinate_Invariant()
    {
        // s = -q - r ⇒ q + r + s == 0 always
        foreach (var q in new[] { -5, -1, 0, 1, 5, 100 })
        foreach (var r in new[] { -5, -1, 0, 1, 5, 100 })
        {
            var h = new HexCoord(q, r);
            Assert.Equal(0, h.Q + h.R + h.S);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]   // self
    [InlineData(0, 0, 1, 0, 1)]   // E
    [InlineData(0, 0, 2, 0, 2)]
    [InlineData(0, 0, 1, -1, 1)]  // NE
    [InlineData(0, 0, 0, 3, 3)]   // SE
    [InlineData(0, 0, -2, 1, 2)]
    [InlineData(0, 0, 3, -2, 3)]  // (3,-2,-1) — max |coord| = 3
    public void DistanceTo_KnownPairs(int q1, int r1, int q2, int r2, int expected)
    {
        var a = new HexCoord(q1, r1);
        var b = new HexCoord(q2, r2);
        Assert.Equal(expected, a.DistanceTo(b));
        Assert.Equal(expected, b.DistanceTo(a)); // symmetric
    }

    [Fact]
    public void DirectionsArray_HasSixUnitVectors()
    {
        Assert.Equal(6, HexCoord.Directions.Length);
        foreach (var d in HexCoord.Directions)
        {
            var origin = new HexCoord(0, 0);
            Assert.Equal(1, origin.DistanceTo(d));
        }
    }

    [Fact]
    public void Neighbour_ByDirection_MatchesDirectionsArray()
    {
        var origin = new HexCoord(0, 0);
        for (var i = 0; i < 6; i++)
        {
            var dir = (HexDirection)i;
            Assert.Equal(HexCoord.Directions[i], origin.Neighbour(dir));
        }
    }

    [Fact]
    public void NeighboursOrder_IsDeterministic()
    {
        var origin = new HexCoord(3, -1);
        var list1 = new System.Collections.Generic.List<HexCoord>(origin.Neighbours());
        var list2 = new System.Collections.Generic.List<HexCoord>(origin.Neighbours());

        Assert.Equal(list1, list2);
    }

    [Fact]
    public void Addition_AndSubtraction_Work()
    {
        var a = new HexCoord(2, -1);
        var b = new HexCoord(1, 3);

        var sum = a + b;
        var diff = a - b;

        Assert.Equal(new HexCoord(3, 2), sum);
        Assert.Equal(new HexCoord(1, -4), diff);
    }
}
