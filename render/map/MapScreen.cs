using Godot;
using System.Numerics;
using System.Text.Json;
using EpochsOfHumanity.Core.Geography;
using EpochsOfHumanity.Core.Visual;
using EpochsOfHumanity.Sim.Biomes;
using EpochsOfHumanity.Sim.Geography;
using GodotVector2 = Godot.Vector2;
using NumericsVector2 = System.Numerics.Vector2;

namespace EpochsOfHumanity.Render.Map;

/// <summary>
/// Main strategic map screen — Levant region, hex tiles, pan/zoom camera.
/// </summary>
/// <remarks>
/// Render-layer entry point for v0.1. Loads biomes from data/biomes/*.json,
/// palette from assets/palettes/paleolithic-base.json, generates the Levant map
/// via <see cref="LevantPreset"/>, and renders it via <see cref="HexMapRenderer"/>.
/// </remarks>
public partial class MapScreen : Node2D
{
    [Export] public float PanSpeed { get; set; } = 1.0f;
    [Export] public float MinZoom { get; set; } = 0.25f;
    [Export] public float MaxZoom { get; set; } = 4.0f;
    [Export] public float ZoomStep { get; set; } = 1.15f;

    private Camera2D? _camera;
    private HexMapRenderer? _renderer;
    private Label? _statusLabel;
    private HexMap? _map;
    private HexLayout _layout = HexLayout.Default;

    private bool _isPanning;
    private GodotVector2 _panStartMouse;
    private GodotVector2 _panStartCamera;

    public override void _Ready()
    {
        _camera = GetNode<Camera2D>("%Camera");
        _renderer = GetNode<HexMapRenderer>("%HexMapRenderer");
        _statusLabel = GetNode<Label>("%StatusLabel");

        try
        {
            LoadAndRender();
            CenterCameraOnStartingHex();
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"MapScreen init failed: {ex.Message}\n{ex.StackTrace}");
            if (_statusLabel != null)
                _statusLabel.Text = $"ERROR: {ex.Message}";
        }
    }

    private void LoadAndRender()
    {
        // 1. Load palette
        var palettePath = ProjectSettings.GlobalizePath("res://assets/palettes/paleolithic-base.json");
        var paletteJson = FileAccess.GetFileAsString(
            FileAccess.FileExists("res://assets/palettes/paleolithic-base.json")
                ? "res://assets/palettes/paleolithic-base.json"
                : palettePath);
        var paletteDto = JsonSerializer.Deserialize<PaletteDto>(paletteJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new System.IO.InvalidDataException("Palette JSON empty");
        var palette = new PaletteRegistry(paletteDto.Id, paletteDto.Colors);

        // 2. Load biomes (all *.json in data/biomes/)
        var biomes = LoadBiomes();
        var biomeRegistry = new BiomeRegistry(biomes);

        // 3. Generate Levant map
        _map = LevantPreset.Build();

        // 4. Render
        _renderer!.Initialize(_map, biomeRegistry, palette, _layout);

        // 5. Status text
        var bounds = _map.Bounds();
        if (_statusLabel != null)
            _statusLabel.Text = $"Levant — {_map.Count} hexes  |  bounds q=[{bounds.MinQ}..{bounds.MaxQ}] r=[{bounds.MinR}..{bounds.MaxR}]";
    }

    private static System.Collections.Generic.List<Biome> LoadBiomes()
    {
        var biomes = new System.Collections.Generic.List<Biome>();
        var dir = DirAccess.Open("res://data/biomes/");
        if (dir == null)
            throw new System.IO.DirectoryNotFoundException("Cannot open res://data/biomes/");

        dir.ListDirBegin();
        var files = new System.Collections.Generic.List<string>();
        while (true)
        {
            var f = dir.GetNext();
            if (string.IsNullOrEmpty(f)) break;
            if (dir.CurrentIsDir()) continue;
            if (!f.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase)) continue;
            files.Add(f);
        }
        dir.ListDirEnd();

        files.Sort(System.StringComparer.Ordinal); // determinism (Law 1)

        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        foreach (var file in files)
        {
            var path = $"res://data/biomes/{file}";
            var json = FileAccess.GetFileAsString(path);
            var biome = JsonSerializer.Deserialize<Biome>(json, jsonOpts)
                ?? throw new System.IO.InvalidDataException($"Empty biome JSON: {file}");
            biomes.Add(biome);
        }
        return biomes;
    }

    private void CenterCameraOnStartingHex()
    {
        if (_camera == null) return;
        var startWorld = _layout.HexToPixel(LevantPreset.StartingHex);
        _camera.Position = new GodotVector2(startWorld.X, startWorld.Y);
        _camera.Zoom = new GodotVector2(2.0f, 2.0f); // zoomed in a bit
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_camera == null || _map == null) return;

        if (@event is InputEventMouseButton mb)
        {
            HandleMouseButton(mb);
        }
        else if (@event is InputEventMouseMotion mm && _isPanning)
        {
            HandleMousePan(mm);
        }
        else if (@event is InputEventKey ke && ke.Pressed && !ke.Echo)
        {
            HandleKeyPress(ke);
        }
    }

    private void HandleMouseButton(InputEventMouseButton mb)
    {
        if (_camera == null) return;

        if (mb.ButtonIndex == MouseButton.Middle || mb.ButtonIndex == MouseButton.Right)
        {
            if (mb.Pressed)
            {
                _isPanning = true;
                _panStartMouse = mb.GlobalPosition;
                _panStartCamera = _camera.Position;
            }
            else
            {
                _isPanning = false;
            }
        }
        else if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
        {
            ZoomBy(ZoomStep);
        }
        else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
        {
            ZoomBy(1.0f / ZoomStep);
        }
        else if (mb.ButtonIndex == MouseButton.Left && mb.Pressed)
        {
            OnHexClicked(mb.GlobalPosition);
        }
    }

    private void HandleMousePan(InputEventMouseMotion mm)
    {
        if (_camera == null) return;
        var delta = mm.GlobalPosition - _panStartMouse;
        // Drag-pan: cursor moves world in opposite direction relative to camera
        _camera.Position = _panStartCamera - delta / _camera.Zoom.X;
    }

    private void HandleKeyPress(InputEventKey ke)
    {
        if (ke.Keycode == Key.Escape)
        {
            GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
        }
    }

    private void ZoomBy(float factor)
    {
        if (_camera == null) return;
        var z = _camera.Zoom.X * factor;
        z = Mathf.Clamp(z, MinZoom, MaxZoom);
        _camera.Zoom = new GodotVector2(z, z);
    }

    private void OnHexClicked(GodotVector2 screenPos)
    {
        if (_camera == null || _map == null || _statusLabel == null) return;

        // Convert screen → world via camera
        var worldPos = _camera.GetGlobalMousePosition();
        var coord = _layout.PixelToHex(new NumericsVector2(worldPos.X, worldPos.Y));

        if (_map.TryGet(coord, out var tile))
        {
            _statusLabel.Text = $"Hex {coord} — biome: {tile.BiomeId}";
            GD.Print($"Clicked hex {coord}: {tile.BiomeId}");
        }
        else
        {
            _statusLabel.Text = $"Hex {coord} — (outside Levant)";
        }
    }
}
