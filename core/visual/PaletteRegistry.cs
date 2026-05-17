using System;
using System.Collections.Generic;

namespace EpochsOfHumanity.Core.Visual;

/// <summary>
/// A named color palette loaded from JSON. Engine-agnostic — uses RGBA tuples,
/// not Godot.Color (Law 2).
/// </summary>
/// <remarks>
/// Render layer converts to Godot.Color at the boundary. See
/// <c>game-visual-style</c> skill for the canonical palette.
/// </remarks>
public sealed class PaletteRegistry
{
    private readonly Dictionary<string, ColorRgba> _colors;

    public string Id { get; }

    public PaletteRegistry(string id, IReadOnlyDictionary<string, string> colorsByName)
    {
        Id = id;
        _colors = new Dictionary<string, ColorRgba>(System.StringComparer.Ordinal);
        foreach (var (name, hex) in colorsByName)
        {
            _colors[name] = ColorRgba.FromHex(hex);
        }
    }

    /// <summary>Looks up a color by name. Throws if not found — fail fast on typos.</summary>
    public ColorRgba this[string name]
        => _colors.TryGetValue(name, out var c)
            ? c
            : throw new KeyNotFoundException(
                $"Color '{name}' not in palette '{Id}'. Available: {string.Join(", ", _colors.Keys)}");

    public bool TryGet(string name, out ColorRgba color) => _colors.TryGetValue(name, out color);

    public int Count => _colors.Count;
}

/// <summary>RGBA color, 0..1 floats. Engine-agnostic.</summary>
public readonly record struct ColorRgba(float R, float G, float B, float A)
{
    public static ColorRgba FromHex(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            throw new System.ArgumentException("Hex string empty");

        var span = hex.AsSpan();
        if (span[0] == '#') span = span[1..];

        if (span.Length != 6 && span.Length != 8)
            throw new System.ArgumentException(
                $"Hex must be #RRGGBB or #RRGGBBAA, got '{hex}'");

        var r = ParseByte(span[0..2]) / 255f;
        var g = ParseByte(span[2..4]) / 255f;
        var b = ParseByte(span[4..6]) / 255f;
        var a = span.Length == 8 ? ParseByte(span[6..8]) / 255f : 1f;
        return new ColorRgba(r, g, b, a);
    }

    private static byte ParseByte(ReadOnlySpan<char> two)
        => byte.Parse(two, System.Globalization.NumberStyles.HexNumber,
                     System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>DTO for JSON deserialization of <c>assets/palettes/*.json</c>.</summary>
public sealed class PaletteDto
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";
    public Dictionary<string, string> Colors { get; set; } = new();
}
