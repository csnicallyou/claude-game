using Godot;
using System.Numerics;
using EpochsOfHumanity.Core.Geography;
using EpochsOfHumanity.Core.Visual;
using EpochsOfHumanity.Sim.Biomes;
using EpochsOfHumanity.Sim.Geography;
using GodotVector2 = Godot.Vector2;
using NumericsVector2 = System.Numerics.Vector2;

namespace EpochsOfHumanity.Render.Map;

/// <summary>
/// Renders a <see cref="HexMap"/> as Godot <see cref="Polygon2D"/> children.
/// One polygon per hex. For v0.1 (~250 hexes) this is fine perf-wise;
/// in v0.3+ we may switch to <see cref="MultiMeshInstance2D"/> for thousands of tiles.
/// </summary>
/// <remarks>
/// This is render-layer code, <c>using Godot</c> is allowed. Reads <c>HexMap</c>
/// and <c>BiomeRegistry</c>, never writes to sim state (Law 2).
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

        // Clear any previous children
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

            // Polygon2D for fill
            var poly = new Polygon2D
            {
                Name = $"Hex_{tile.Coord.Q}_{tile.Coord.R}",
                Position = ToGodot(center),
                Polygon = cornerOffsetsGodot,
                Color = baseColor,
            };
            AddChild(poly);

            // Thin outline as child Line2D
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
        }
    }

    private static GodotVector2 ToGodot(NumericsVector2 v) => new(v.X, v.Y);

    private static Color ToGodotColor(ColorRgba c) => new(c.R, c.G, c.B, c.A);
}
