using Godot;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using EpochsOfHumanity.Core.Geography;
using EpochsOfHumanity.Core.Visual;
using EpochsOfHumanity.Sim.Biomes;
using EpochsOfHumanity.Sim.Characters;
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
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Export] public float PanSpeed { get; set; } = 1.0f;
    [Export] public float MinZoom { get; set; } = 0.25f;
    [Export] public float MaxZoom { get; set; } = 4.0f;
    [Export] public float ZoomStep { get; set; } = 1.15f;

    private Camera2D? _camera;
    private HexMapRenderer? _renderer;
    private RiverRenderer? _riverRenderer;
    private LandmarkLabels? _landmarks;
    private Node2D? _tribesLayer;
    private Label? _statusLabel;
    private HexMap? _map;
    private TribeRegistry? _tribes;
    private PaletteRegistry? _palette;
    private HexLayout _layout = HexLayout.Default;

    private bool _isPanning;
    private GodotVector2 _panStartMouse;
    private GodotVector2 _panStartCamera;

    public override void _Ready()
    {
        _camera = GetNode<Camera2D>("%Camera");
        _renderer = GetNode<HexMapRenderer>("%HexMapRenderer");
        _riverRenderer = GetNode<RiverRenderer>("%RiverRenderer");
        _landmarks = GetNode<LandmarkLabels>("%LandmarkLabels");
        _tribesLayer = GetNode<Node2D>("%TribesLayer");
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
        var paletteDto = JsonSerializer.Deserialize<PaletteDto>(paletteJson, JsonOpts)
            ?? throw new System.IO.InvalidDataException("Palette JSON empty");
        _palette = new PaletteRegistry(paletteDto.Id, paletteDto.Colors);

        // 2. Load biomes (all *.json in data/biomes/)
        var biomes = LoadBiomes();
        var biomeRegistry = new BiomeRegistry(biomes);

        // 3. Generate Levant map
        _map = LevantPreset.Build();

        // 4. Render hexes + pictograms
        _renderer!.Initialize(_map, biomeRegistry, _palette, _layout);

        // 5. Rivers
        _riverRenderer?.Initialize(LevantRivers.All(), _layout, _palette);

        // 6. Landmark labels overlay
        _landmarks?.Initialize(_layout);

        // 7. Tribes (player + NPC)
        _tribes = LevantTribesPreset.Build();
        RenderTribes();

        // 8. Status text
        var playerTribe = _tribes.Player;
        if (_statusLabel != null)
            _statusLabel.Text = $"Levant — {_map.Count} hexes  |  Your tribe: {playerTribe.Name} (Mt. Carmel)  |  {_tribes.Count - 1} neighbouring tribes  |  45,000 BP";
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

        foreach (var file in files)
        {
            var path = $"res://data/biomes/{file}";
            var json = FileAccess.GetFileAsString(path);
            var biome = JsonSerializer.Deserialize<Biome>(json, JsonOpts)
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

    private void RenderTribes()
    {
        if (_tribesLayer == null || _tribes == null || _palette == null) return;
        foreach (var c in _tribesLayer.GetChildren()) c.QueueFree();

        foreach (var tribe in _tribes.All)
        {
            var pos = _layout.HexToPixel(tribe.HomeHex);
            var marker = new SettlementMarker
            {
                Name = $"Tribe_{tribe.Id}",
                Position = new GodotVector2(pos.X, pos.Y),
            };
            _tribesLayer.AddChild(marker);
            marker.Configure(tribe.Name, tribe.Species, tribe.IsPlayerControlled, _palette);
        }
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
            var tribe = _tribes?.AtHex(coord);
            if (tribe != null)
            {
                var speciesLabel = tribe.Species == Species.Sapiens ? "Sapiens"
                    : tribe.Species == Species.Neanderthal ? "Neanderthal"
                    : "Denisovan";
                var marker = tribe.IsPlayerControlled ? " ★" : "";
                var sex = tribe.Chief.Sex == Sex.Male ? "♂" : "♀";
                _statusLabel.Text =
                    $"Hex {coord} — {tribe.Name} ({speciesLabel}){marker}  |  " +
                    $"chief {tribe.Chief.Name} {sex} {tribe.Chief.AgeWinters} winters  |  " +
                    $"biome: {tile.BiomeId}";
            }
            else
            {
                _statusLabel.Text = $"Hex {coord} — biome: {tile.BiomeId}";
            }
            GD.Print($"Clicked hex {coord}: {tile.BiomeId}, tribe={tribe?.Name ?? "none"}");
        }
        else
        {
            _statusLabel.Text = $"Hex {coord} — (outside Levant)";
        }
    }
}
