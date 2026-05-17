using System.Numerics;
using EpochsOfHumanity.Core.Geography;
using Xunit;

namespace EpochsOfHumanity.Tests.Core.Geography;

public class HexLayoutTests
{
    [Fact]
    public void Origin_HexAtZeroZero_PixelAtOrigin()
    {
        var layout = new HexLayout(Size: 20.0, Origin: new Vector2(100, 50));
        var px = layout.HexToPixel(new HexCoord(0, 0));

        Assert.Equal(100f, px.X, precision: 3);
        Assert.Equal(50f, px.Y, precision: 3);
    }

    [Fact]
    public void HexToPixel_RoundTrip_RestoresHex()
    {
        var layout = HexLayout.Default;

        // Check a grid of hexes — pixel-to-hex round-trip should recover original
        for (var q = -10; q <= 10; q++)
        for (var r = -10; r <= 10; r++)
        {
            var original = new HexCoord(q, r);
            var px = layout.HexToPixel(original);
            var recovered = layout.PixelToHex(px);
            Assert.Equal(original, recovered);
        }
    }

    [Fact]
    public void Corners_AreSixVectorsAtSizeDistance()
    {
        var layout = new HexLayout(Size: 30.0, Origin: Vector2.Zero);
        var corners = layout.Corners();

        Assert.Equal(6, corners.Length);
        foreach (var c in corners)
        {
            // Length should approximately equal Size
            var len = System.Math.Sqrt(c.X * c.X + c.Y * c.Y);
            Assert.InRange(len, 29.9, 30.1);
        }
    }

    [Fact]
    public void DefaultLayout_HasReasonableSize()
    {
        // Our 64×64 tile uses Size ≈ 18.475 (so bounding box ≈ 64 px wide)
        var layout = HexLayout.Default;
        Assert.InRange(layout.Size, 18.0, 19.0);
    }
}
