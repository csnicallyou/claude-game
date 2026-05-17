using Godot;
using System.Numerics;
using EpochsOfHumanity.Core.Geography;
using GodotVector2 = Godot.Vector2;

namespace EpochsOfHumanity.Render.Map;

/// <summary>
/// Adds named landmark labels on top of the strategic map (mountains, rivers, regions).
/// Latin transliteration only, per CLAUDE.md §7 (archaeological names not translated).
/// </summary>
public partial class LandmarkLabels : Node2D
{
    private HexLayout _layout = HexLayout.Default;

    public void Initialize(HexLayout layout)
    {
        _layout = layout;
        Render();
    }

    private void Render()
    {
        foreach (var c in GetChildren()) c.QueueFree();

        // Each landmark: name, anchor axial coord (approximate), font size, color modulate alpha
        var landmarks = new (string Name, HexCoord Anchor, int FontSize, float Alpha)[]
        {
            ("Lebanon",      new HexCoord( 0, -8),  16, 0.85f),
            ("Mt. Hermon",   new HexCoord( 1, -5),  12, 0.85f),
            ("Mt. Carmel",   new HexCoord(-3, -1),  12, 0.85f),
            ("Jordan",       new HexCoord(-1,  1),  11, 0.75f),
            ("Galilee",      new HexCoord( 0, -3),  11, 0.7f),
            ("Negev",        new HexCoord(-2,  5),  13, 0.8f),
            ("Sinai",        new HexCoord(-3,  9),  14, 0.85f),
            ("Zagros",       new HexCoord( 6, -2),  14, 0.85f),
            ("Mediterranean", new HexCoord(-7, 1),  12, 0.75f),
        };

        foreach (var lm in landmarks)
        {
            var pos = _layout.HexToPixel(lm.Anchor);
            var lbl = new Label
            {
                Name = $"Landmark_{lm.Name}",
                Text = lm.Name,
                Position = new GodotVector2(pos.X - 60, pos.Y - 8),
                Size = new GodotVector2(120, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.95f, 0.92f, 0.85f, lm.Alpha),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            lbl.AddThemeFontSizeOverride("font_size", lm.FontSize);
            AddChild(lbl);
        }
    }
}
