using Godot;
using EpochsOfHumanity.Core.Visual;
using EpochsOfHumanity.Sim.Characters;
using GodotVector2 = Godot.Vector2;

namespace EpochsOfHumanity.Render.Map;

/// <summary>
/// Visual marker for a tribe settlement on the strategic map.
/// </summary>
/// <remarks>
/// Visual styling differs by Species:
/// - <see cref="Species.Sapiens"/>: pointed tent + smoke (general dwelling).
/// - <see cref="Species.Neanderthal"/>: rounder shelter + cave-mouth motif (anatomical-adapted).
/// - <see cref="Species.Denisovan"/>: rare wanderers, single hut form.
///
/// Player-controlled tribe gets a subtle highlight ring.
///
/// Placeholders for v0.1. Will be replaced by real pixel-art tents/structures.
/// </remarks>
public partial class SettlementMarker : Node2D
{
    private PaletteRegistry? _palette;
    private string _label = "";
    private Species _species = Species.Sapiens;
    private bool _isPlayer;
    private Label? _labelNode;

    public void Configure(string label, Species species, bool isPlayer, PaletteRegistry palette)
    {
        _label = label;
        _species = species;
        _isPlayer = isPlayer;
        _palette = palette;
        QueueRedraw();
        BuildLabel();
    }

    public override void _Draw()
    {
        if (_palette is null) return;

        switch (_species)
        {
            case Species.Sapiens:      DrawSapiensCamp(_palette); break;
            case Species.Neanderthal:  DrawNeanderthalCamp(_palette); break;
            case Species.Denisovan:    DrawDenisovanCamp(_palette); break;
        }

        if (_isPlayer) DrawPlayerHighlight(_palette);
    }

    private void DrawSapiensCamp(PaletteRegistry palette)
    {
        var tent = ToColor(palette["mammoth-brown"]);
        var tentDark = ToColor(palette["charcoal"]);
        var smoke = ToColor(palette["smoke-grey"]);
        var fire = ToColor(palette["hearth-orange"]);

        // Pointed tent
        DrawColoredPolygon(new GodotVector2[]
        {
            new(0, -14), new(-10, 5), new(10, 5),
        }, tent);
        // Shade on right
        DrawColoredPolygon(new GodotVector2[]
        {
            new(0, -14), new(0, 5), new(10, 5),
        }, new Color(tent.R * 0.75f, tent.G * 0.75f, tent.B * 0.75f, 1));

        // Dark opening
        DrawColoredPolygon(new GodotVector2[]
        {
            new(-2, 5), new(2, 5), new(0, -2),
        }, tentDark);

        // Fire + smoke
        DrawCircle(new GodotVector2(-13, 5), 3f, fire);
        DrawCircle(new GodotVector2(-13, -1), 2.5f, smoke);
        DrawCircle(new GodotVector2(-15, -6), 1.8f, new Color(smoke.R, smoke.G, smoke.B, 0.6f));
    }

    private void DrawNeanderthalCamp(PaletteRegistry palette)
    {
        var hide = ToColor(palette["fur-tan"]);
        var dark = ToColor(palette["ash-grey"]);
        var cave = ToColor(palette["charcoal"]);
        var fire = ToColor(palette["hearth-orange"]);

        // Cave-mouth shape (rounded, lower, wider)
        DrawColoredPolygon(new GodotVector2[]
        {
            new(-11, 5), new(-11, -2), new(-7, -7), new(7, -7), new(11, -2), new(11, 5),
        }, hide);
        // Shade
        DrawColoredPolygon(new GodotVector2[]
        {
            new(0, -7), new(7, -7), new(11, -2), new(11, 5), new(0, 5),
        }, new Color(hide.R * 0.75f, hide.G * 0.75f, hide.B * 0.75f, 1));

        // Cave entrance (dark arch)
        DrawColoredPolygon(new GodotVector2[]
        {
            new(-4, 5), new(-4, -1), new(-2, -3), new(2, -3), new(4, -1), new(4, 5),
        }, cave);

        // Bone marker on top (neanderthal symbolic placeholder)
        DrawLine(new GodotVector2(0, -8), new GodotVector2(0, -12), dark, 1.5f);
        DrawCircle(new GodotVector2(0, -13), 1.5f, dark);

        // Fire
        DrawCircle(new GodotVector2(-14, 5), 2.5f, fire);
    }

    private void DrawDenisovanCamp(PaletteRegistry palette)
    {
        var hut = ToColor(palette["fur-tan"]);
        var dark = ToColor(palette["charcoal"]);
        // Simple low hut — placeholder
        DrawColoredPolygon(new GodotVector2[]
        {
            new(-8, 4), new(-6, -4), new(6, -4), new(8, 4),
        }, hut);
        DrawColoredPolygon(new GodotVector2[]
        {
            new(-2, 4), new(2, 4), new(0, -1),
        }, dark);
    }

    private void DrawPlayerHighlight(PaletteRegistry palette)
    {
        var ring = ToColor(palette["fire-yellow"]);
        DrawArc(GodotVector2.Zero, 18f, 0f, Mathf.Pi * 2f, 48,
                new Color(ring.R, ring.G, ring.B, 0.7f), 1.5f);
    }

    private void BuildLabel()
    {
        if (_labelNode is null)
        {
            _labelNode = new Label
            {
                Name = "Name",
                Text = _label,
                Position = new GodotVector2(-60, 10),
                Size = new GodotVector2(120, 18),
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(1, 1, 1, _isPlayer ? 1.0f : 0.85f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _labelNode.AddThemeFontSizeOverride("font_size", _isPlayer ? 12 : 10);
            AddChild(_labelNode);
        }
        else
        {
            _labelNode.Text = _label;
        }
    }

    private static Color ToColor(ColorRgba c) => new(c.R, c.G, c.B, c.A);
}
