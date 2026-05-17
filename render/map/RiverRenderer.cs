using Godot;
using System.Collections.Generic;
using EpochsOfHumanity.Core.Geography;
using EpochsOfHumanity.Core.Visual;
using EpochsOfHumanity.Sim.Geography;
using GodotVector2 = Godot.Vector2;

namespace EpochsOfHumanity.Render.Map;

/// <summary>
/// Draws rivers as Line2D overlays on the strategic map.
/// </summary>
/// <remarks>
/// One Line2D per river, points = hex centres of the river's path.
/// Drawn ABOVE biome fills but BELOW settlement markers (z-ordering via scene tree).
/// </remarks>
public partial class RiverRenderer : Node2D
{
    private HexLayout _layout = HexLayout.Default;
    private PaletteRegistry? _palette;

    public void Initialize(IEnumerable<River> rivers, HexLayout layout, PaletteRegistry palette)
    {
        _layout = layout;
        _palette = palette;

        foreach (var c in GetChildren()) c.QueueFree();

        var riverColor = ToColor(palette["river-blue"]);

        foreach (var river in rivers)
        {
            var line = new Line2D
            {
                Name = $"River_{river.Id}",
                Width = 3.5f,
                DefaultColor = riverColor,
                Antialiased = true,
                JointMode = Line2D.LineJointMode.Round,
                BeginCapMode = Line2D.LineCapMode.Round,
                EndCapMode = Line2D.LineCapMode.Round,
            };

            foreach (var coord in river.Path)
            {
                var p = _layout.HexToPixel(coord);
                line.AddPoint(new GodotVector2(p.X, p.Y));
            }
            AddChild(line);
        }
    }

    private static Color ToColor(ColorRgba c) => new(c.R, c.G, c.B, c.A);
}
