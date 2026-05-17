using Godot;
using EpochsOfHumanity.Core.Visual;
using GodotVector2 = Godot.Vector2;

namespace EpochsOfHumanity.Render.Map;

/// <summary>
/// Visual marker for a settlement on the strategic map: tent silhouette,
/// smoke wisp, and an optional name label.
/// </summary>
/// <remarks>
/// Placeholder until v0.3 brings real settlement entities + pixel art.
/// For now, generated procedurally from palette colors.
/// </remarks>
public partial class SettlementMarker : Node2D
{
    private PaletteRegistry? _palette;
    private string _label = "";
    private Label? _labelNode;

    public void Configure(string label, PaletteRegistry palette)
    {
        _palette = palette;
        _label = label;
        QueueRedraw();
        BuildLabel();
    }

    public override void _Draw()
    {
        if (_palette is null) return;

        var tentColor = ToColor(_palette["mammoth-brown"]);
        var tentDark = ToColor(_palette["charcoal"]);
        var smokeColor = ToColor(_palette["smoke-grey"]);
        var fireColor = ToColor(_palette["hearth-orange"]);

        // Tent (large triangle)
        var tent = new[]
        {
            new GodotVector2(0, -16),
            new GodotVector2(-12, 6),
            new GodotVector2(12, 6),
        };
        DrawColoredPolygon(tent, tentColor);
        // Tent shading (right side darker)
        var tentShade = new[]
        {
            new GodotVector2(0, -16),
            new GodotVector2(0, 6),
            new GodotVector2(12, 6),
        };
        DrawColoredPolygon(tentShade, new Color(tentColor.R * 0.75f, tentColor.G * 0.75f, tentColor.B * 0.75f, 1));

        // Tent opening (small dark triangle in front)
        var opening = new[]
        {
            new GodotVector2(-3, 6), new GodotVector2(3, 6), new GodotVector2(0, -3),
        };
        DrawColoredPolygon(opening, tentDark);

        // Fire at base
        DrawCircle(new GodotVector2(-16, 6), 3.5f, fireColor);
        // smoke rising
        DrawCircle(new GodotVector2(-16, -2), 2.5f, smokeColor);
        DrawCircle(new GodotVector2(-18, -8), 2f, new Color(smokeColor.R, smokeColor.G, smokeColor.B, 0.6f));
        DrawCircle(new GodotVector2(-14, -14), 1.5f, new Color(smokeColor.R, smokeColor.G, smokeColor.B, 0.4f));
    }

    private void BuildLabel()
    {
        if (_labelNode is null)
        {
            _labelNode = new Label
            {
                Name = "Name",
                Text = _label,
                Position = new GodotVector2(-50, 12),
                Size = new GodotVector2(100, 18),
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(1, 1, 1, 0.95f),
            };
            _labelNode.AddThemeFontSizeOverride("font_size", 11);
            AddChild(_labelNode);
        }
        else
        {
            _labelNode.Text = _label;
        }
    }

    private static Color ToColor(ColorRgba c) => new(c.R, c.G, c.B, c.A);
}
