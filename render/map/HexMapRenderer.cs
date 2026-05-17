using Godot;
using System.Numerics;
using EpochsOfHumanity.Core.Geography;
using EpochsOfHumanity.Core.Prng;
using EpochsOfHumanity.Core.Visual;
using EpochsOfHumanity.Sim.Biomes;
using EpochsOfHumanity.Sim.Geography;
using GodotVector2 = Godot.Vector2;
using NumericsVector2 = System.Numerics.Vector2;

namespace EpochsOfHumanity.Render.Map;

/// <summary>
/// Renders a <see cref="HexMap"/> as Godot <see cref="Polygon2D"/> children,
/// plus deterministic biome pictograms on each tile.
/// </summary>
/// <remarks>
/// One Polygon2D per hex (fill), one Line2D child (border), 0..N PictogramNode
/// children per hex (decorations). For v0.1 (~200 hexes × few pictograms each)
/// this is fine perf-wise.
///
/// Pictogram placement is deterministic: seeded by hex coord + biome id, so the
/// same map always shows the same decorations (Law 1).
/// </remarks>
public partial class HexMapRenderer : Node2D
{
    private HexMap? _map;
    private BiomeRegistry? _biomes;
    private PaletteRegistry? _palette;
    private HexLayout _layout = HexLayout.Default;

    /// <summary>Border color around each hex (thin outline for grid visibility).</summary>
    public Color BorderColor { get; set; } = new(0.05f, 0.05f, 0.05f, 0.6f);

    /// <summary>Width of hex border in pixels.</summary>
    public float BorderWidth { get; set; } = 1.0f;

    public void Initialize(HexMap map, BiomeRegistry biomes, PaletteRegistry palette, HexLayout layout)
    {
        _map = map;
        _biomes = biomes;
        _palette = palette;
        _layout = layout;
        Render();
    }

    private void Render()
    {
        if (_map is null || _biomes is null || _palette is null) return;

        foreach (var child in GetChildren())
            child.QueueFree();

        var cornerOffsetsNumerics = _layout.Corners();
        var cornerOffsetsGodot = new GodotVector2[6];
        for (var i = 0; i < 6; i++)
            cornerOffsetsGodot[i] = ToGodot(cornerOffsetsNumerics[i]);

        foreach (var tile in _map.AllOrdered())
        {
            var center = _layout.HexToPixel(tile.Coord);
            var biome = _biomes.Get(tile.BiomeId);
            var baseColor = ToGodotColor(_palette[biome.BaseColor]);

            var poly = new Polygon2D
            {
                Name = $"Hex_{tile.Coord.Q}_{tile.Coord.R}",
                Position = ToGodot(center),
                Polygon = cornerOffsetsGodot,
                Color = baseColor,
            };
            AddChild(poly);

            // Border outline
            if (BorderWidth > 0f)
            {
                var line = new Line2D
                {
                    Name = "Border",
                    Width = BorderWidth,
                    DefaultColor = BorderColor,
                    Closed = true,
                };
                foreach (var c in cornerOffsetsGodot)
                    line.AddPoint(c);
                poly.AddChild(line);
            }

            // Deterministic pictograms based on biome config
            AddPictograms(poly, tile, biome);
        }
    }

    private void AddPictograms(Polygon2D hexNode, HexTile tile, Biome biome)
    {
        if (_palette is null) return;

        // Per-hex PRNG: deterministic from coord + biome id
        var rng = new Rng($"hex-pictograms-{tile.Coord.Q}-{tile.Coord.R}-{tile.BiomeId}");

        var maxPictogramsPerHex = 4;
        var placed = 0;

        foreach (var pg in biome.Pictograms)
        {
            if (placed >= maxPictogramsPerHex) break;
            if (!rng.Chance(pg.Weight)) continue;

            // Random offset within hex (stay inside the inscribed circle)
            var size = _layout.Size;
            var maxR = size * 0.55;
            var angle = rng.NextDouble() * System.Math.PI * 2.0;
            var radius = rng.NextDouble() * maxR;
            var ox = System.Math.Cos(angle) * radius;
            var oy = System.Math.Sin(angle) * radius;

            var node = new PictogramNode
            {
                Name = $"pg_{placed}_{pg.Key}",
                Position = new GodotVector2((float)ox, (float)oy),
            };
            hexNode.AddChild(node);
            node.Configure(pg.Key, _palette);
            placed++;
        }
    }

    private static GodotVector2 ToGodot(NumericsVector2 v) => new(v.X, v.Y);

    private static Color ToGodotColor(ColorRgba c) => new(c.R, c.G, c.B, c.A);
}
