using Godot;
using EpochsOfHumanity.Core.Visual;
using GodotVector2 = Godot.Vector2;

namespace EpochsOfHumanity.Render.Map;

/// <summary>
/// Draws a single placeholder pictogram (tree, rock, cave, etc.) at the origin of this Node2D.
/// </summary>
/// <remarks>
/// V0.1 placeholders — simple geometric shapes by key. Will be replaced by real
/// pixel sprites later. All shapes are scaled to fit nicely inside a 40x40 px bounding box
/// so they read at default zoom on the hex.
/// </remarks>
public partial class PictogramNode : Node2D
{
    private string _key = "";
    private PaletteRegistry? _palette;

    public void Configure(string pictogramKey, PaletteRegistry palette)
    {
        _key = pictogramKey;
        _palette = palette;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_palette is null || string.IsNullOrEmpty(_key)) return;
        DrawByKey(_key, _palette);
    }

    private void DrawByKey(string key, PaletteRegistry palette)
    {
        switch (key)
        {
            // --- Trees ---
            case "tree-oak":          DrawTree(palette["moss-green"],   palette["mammoth-brown"], 8f, 6f); break;
            case "tree-pistachio":    DrawTree(palette["spring-green"], palette["mammoth-brown"], 7f, 5f); break;
            case "tree-cedar":        DrawCedar(palette["moss-green"],  palette["mammoth-brown"]); break;
            case "tree-conifer":      DrawCedar(palette["moss-green"],  palette["mammoth-brown"]); break;
            case "acacia-tree":       DrawAcacia(palette["spring-green"], palette["mammoth-brown"]); break;

            // --- Rocks / boulders ---
            case "rock-small":        DrawRock(palette["flint-grey"], 4f); break;
            case "rock-large":        DrawRock(palette["flint-grey"], 7f); break;
            case "boulder":           DrawRock(palette["ash-grey"],   8f); break;
            case "wadi-rock":         DrawWadiRock(palette["flint-grey"]); break;

            // --- Caves ---
            case "cave-entrance":     DrawCave(palette["charcoal"]); break;

            // --- Water / wetland ---
            case "reed-cluster":      DrawReeds(palette["spring-green"]); break;
            case "papyrus":           DrawPapyrus(palette["spring-green"]); break;
            case "fishing-bird":      DrawBird(palette["charcoal"]); break;

            // --- Coast ---
            case "shell-cluster":     DrawShells(palette["bone-cream"]); break;
            case "driftwood":         DrawDriftwood(palette["mammoth-brown"]); break;

            // --- Vegetation ---
            case "sparse-shrub":      DrawShrub(palette["moss-green"]); break;

            // Unknown key: tiny dot in shadow color so we notice but don't crash
            default:                  DrawCircle(GodotVector2.Zero, 1.5f, new Color(1, 0, 1, 0.5f)); break;
        }
    }

    private void DrawTree(ColorRgba canopy, ColorRgba trunk, float canopyRadius, float trunkHeight)
    {
        var trunkColor = ToColor(trunk);
        var canopyColor = ToColor(canopy);
        // trunk
        DrawRect(new Rect2(-1, 0, 2, trunkHeight), trunkColor, true);
        // canopy
        DrawCircle(new GodotVector2(0, -canopyRadius / 2f), canopyRadius, canopyColor);
    }

    private void DrawCedar(ColorRgba canopy, ColorRgba trunk)
    {
        var canopyColor = ToColor(canopy);
        var trunkColor = ToColor(trunk);
        DrawRect(new Rect2(-1, 2, 2, 5), trunkColor, true);
        var pts = new GodotVector2[]
        {
            new(0, -9), new(-5, 2), new(5, 2),
        };
        DrawColoredPolygon(pts, canopyColor);
    }

    private void DrawAcacia(ColorRgba canopy, ColorRgba trunk)
    {
        var canopyColor = ToColor(canopy);
        var trunkColor = ToColor(trunk);
        // trunk
        DrawRect(new Rect2(-1, -2, 2, 8), trunkColor, true);
        // flat canopy
        DrawColoredPolygon(new GodotVector2[]
        {
            new(-7, -3), new(7, -3), new(5, -1), new(-5, -1),
        }, canopyColor);
    }

    private void DrawRock(ColorRgba color, float size)
    {
        var c = ToColor(color);
        DrawCircle(GodotVector2.Zero, size, c);
        // tiny darker spot for shading
        DrawCircle(new GodotVector2(size * 0.3f, size * 0.3f), size * 0.4f,
                   new Color(c.R * 0.7f, c.G * 0.7f, c.B * 0.7f, c.A));
    }

    private void DrawWadiRock(ColorRgba color)
    {
        var c = ToColor(color);
        DrawColoredPolygon(new GodotVector2[]
        {
            new(-6, 1), new(-2, -2), new(3, -1), new(6, 2), new(2, 3), new(-3, 3),
        }, c);
    }

    private void DrawCave(ColorRgba color)
    {
        var c = ToColor(color);
        // dark arch
        DrawColoredPolygon(new GodotVector2[]
        {
            new(-6, 4), new(-6, -1), new(-3, -4), new(3, -4), new(6, -1), new(6, 4),
        }, c);
    }

    private void DrawReeds(ColorRgba color)
    {
        var c = ToColor(color);
        for (var i = -2; i <= 2; i++)
        {
            DrawLine(new GodotVector2(i * 2.5f, 5), new GodotVector2(i * 2.5f, -6), c, 1f);
        }
    }

    private void DrawPapyrus(ColorRgba color)
    {
        var c = ToColor(color);
        // tall stem
        DrawLine(new GodotVector2(0, 8), new GodotVector2(0, -8), c, 1.5f);
        // umbrella top
        for (var i = 0; i < 5; i++)
        {
            var angle = -Mathf.Pi + i * Mathf.Pi / 4f;
            var end = new GodotVector2(Mathf.Cos(angle) * 4, Mathf.Sin(angle) * 4 - 6);
            DrawLine(new GodotVector2(0, -8), end, c, 1f);
        }
    }

    private void DrawBird(ColorRgba color)
    {
        var c = ToColor(color);
        // m-shape: simple bird silhouette
        DrawLine(new GodotVector2(-5, 1), new GodotVector2(-2, -2), c, 1.2f);
        DrawLine(new GodotVector2(-2, -2), new GodotVector2(0, 0), c, 1.2f);
        DrawLine(new GodotVector2(0, 0), new GodotVector2(2, -2), c, 1.2f);
        DrawLine(new GodotVector2(2, -2), new GodotVector2(5, 1), c, 1.2f);
    }

    private void DrawShells(ColorRgba color)
    {
        var c = ToColor(color);
        DrawCircle(new GodotVector2(-3, 0), 1.5f, c);
        DrawCircle(new GodotVector2(2, -2), 1.5f, c);
        DrawCircle(new GodotVector2(3, 2), 1.5f, c);
    }

    private void DrawDriftwood(ColorRgba color)
    {
        var c = ToColor(color);
        DrawLine(new GodotVector2(-6, 0), new GodotVector2(6, 1), c, 2f);
    }

    private void DrawShrub(ColorRgba color)
    {
        var c = ToColor(color);
        DrawCircle(new GodotVector2(-2, 0), 2.5f, c);
        DrawCircle(new GodotVector2(2, -1), 2f, c);
    }

    private static Color ToColor(ColorRgba c) => new(c.R, c.G, c.B, c.A);
}
