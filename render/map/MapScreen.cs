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
/// Main strategic map screen — Levant region, hex tiles, pan/zoom camera,
/// year advancement and basic chief aging.
/// </summary>
/// <remarks>
/// Render-layer entry point for v0.1. V0.2 will move year/aging behind a proper
/// GameWorld + Command pipeline; for now the local state here is acceptable
/// because the actual simulation systems aren't wired yet.
/// </remarks>
public partial class MapScreen : Node2D
{
    private const int StartYearBP = 45_000;

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
    private Label? _yearLabel;
    private Label? _tribeLabel;
    private Button? _nextYearButton;
    private HexMap? _map;
    private TribeRegistry? _tribes;
    private PaletteRegistry? _palette;
    private HexLayout _layout = HexLayout.Default;

    /// <summary>Years elapsed since game start. Chief age = base + this.</summary>
    private int _yearsElapsed;

    private bool _isPanning;
    private GodotVector2 _panStartMouse;
    private GodotVector2 _panStartCamera;
    private HexCoord? _selectedHex;

    public override void _Ready()
    {
        _camera = GetNode<Camera2D>("%Camera");
        _renderer = GetNode<HexMapRenderer>("%HexMapRenderer");
        _riverRenderer = GetNode<RiverRenderer>("%RiverRenderer");
        _landmarks = GetNode<LandmarkLabels>("%LandmarkLabels");
        _tribesLayer = GetNode<Node2D>("%TribesLayer");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _yearLabel = GetNode<Label>("%YearLabel");
        _tribeLabel = GetNode<Label>("%TribeLabel");
        _nextYearButton = GetNode<Button>("%NextYearButton");
        _nextYearButton.Pressed += OnNextYearPressed;

        try
        {
            LoadAndRender();
            CenterCameraOnStartingHex();
            RefreshHud();
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
        var paletteJson = FileAccess.GetFileAsString("res://assets/palettes/paleolithic-base.json");
        var paletteDto = JsonSerializer.Deserialize<PaletteDto>(paletteJson, JsonOpts)
            ?? throw new System.IO.InvalidDataException("Palette JSON empty");
        _palette = new PaletteRegistry(paletteDto.Id, paletteDto.Colors);

        // 2. Load biomes
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
    }

    private static List<Biome> LoadBiomes()
    {
        var biomes = new List<Biome>();
        var dir = DirAccess.Open("res://data/biomes/")
            ?? throw new System.IO.DirectoryNotFoundException("Cannot open res://data/biomes/");

        dir.ListDirBegin();
        var files = new List<string>();
        while (true)
        {
            var f = dir.GetNext();
            if (string.IsNullOrEmpty(f)) break;
            if (dir.CurrentIsDir()) continue;
            if (!f.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase)) continue;
            files.Add(f);
        }
        dir.ListDirEnd();

        files.Sort(System.StringComparer.Ordinal);

        foreach (var file in files)
        {
            var json = FileAccess.GetFileAsString($"res://data/biomes/{file}");
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
        _camera.Zoom = new GodotVector2(2.0f, 2.0f);
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

    // ---------- Year cycle ----------

    private void OnNextYearPressed()
    {
        _yearsElapsed++;
        RefreshHud();
        UpdateSelectedHexStatus();
    }

    private int CurrentYearBP => StartYearBP - _yearsElapsed;

    private int EffectiveAge(Chief chief) => chief.AgeWinters + _yearsElapsed;

    private void RefreshHud()
    {
        if (_yearLabel != null)
            _yearLabel.Text = $"{CurrentYearBP:N0} BP";

        if (_tribeLabel != null && _tribes != null)
        {
            var player = _tribes.Player;
            _tribeLabel.Text =
                $"{player.Name} · chief {player.Chief.Name}, {EffectiveAge(player.Chief)} winters";
        }

        if (_statusLabel != null && _map != null && _tribes != null)
        {
            _statusLabel.Text =
                $"Levant — {_map.Count} hexes  |  " +
                $"{_tribes.Count - 1} neighbouring tribes  |  " +
                $"year {CurrentYearBP:N0} BP  ({_yearsElapsed} winters lived)";
        }
    }

    // ---------- Input ----------

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_camera == null || _map == null) return;

        if (@event is InputEventMouseButton mb) HandleMouseButton(mb);
        else if (@event is InputEventMouseMotion mm && _isPanning) HandleMousePan(mm);
        else if (@event is InputEventKey ke && ke.Pressed && !ke.Echo) HandleKeyPress(ke);
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
            OnHexClicked();
        }
    }

    private void HandleMousePan(InputEventMouseMotion mm)
    {
        if (_camera == null) return;
        var delta = mm.GlobalPosition - _panStartMouse;
        _camera.Position = _panStartCamera - delta / _camera.Zoom.X;
    }

    private void HandleKeyPress(InputEventKey ke)
    {
        if (ke.Keycode == Key.Escape)
        {
            GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
        }
        else if (ke.Keycode == Key.Enter || ke.Keycode == Key.KpEnter || ke.Keycode == Key.Space)
        {
            OnNextYearPressed();
        }
    }

    private void ZoomBy(float factor)
    {
        if (_camera == null) return;
        var z = _camera.Zoom.X * factor;
        z = Mathf.Clamp(z, MinZoom, MaxZoom);
        _camera.Zoom = new GodotVector2(z, z);
    }

    private void OnHexClicked()
    {
        if (_camera == null || _map == null || _statusLabel == null) return;

        var worldPos = _camera.GetGlobalMousePosition();
        _selectedHex = _layout.PixelToHex(new NumericsVector2(worldPos.X, worldPos.Y));
        UpdateSelectedHexStatus();
    }

    private void UpdateSelectedHexStatus()
    {
        if (_statusLabel == null || _map == null || _selectedHex == null) return;
        var coord = _selectedHex.Value;

        if (!_map.TryGet(coord, out var tile))
        {
            _statusLabel.Text = $"Hex {coord} — (outside Levant)  |  year {CurrentYearBP:N0} BP";
            return;
        }

        var tribe = _tribes?.AtHex(coord);
        if (tribe != null)
        {
            var speciesLabel = tribe.Species == Species.Sapiens ? "Sapiens"
                : tribe.Species == Species.Neanderthal ? "Neanderthal"
                : "Denisovan";
            var marker = tribe.IsPlayerControlled ? " ★" : "";
            var sex = tribe.Chief.Sex == Sex.Male ? "♂" : "♀";
            var age = EffectiveAge(tribe.Chief);
            _statusLabel.Text =
                $"Hex {coord} — {tribe.Name} ({speciesLabel}){marker}  |  " +
                $"chief {tribe.Chief.Name} {sex} {age} winters  |  " +
                $"biome: {tile.BiomeId}  |  year {CurrentYearBP:N0} BP";
        }
        else
        {
            _statusLabel.Text =
                $"Hex {coord} — biome: {tile.BiomeId}  |  year {CurrentYearBP:N0} BP";
        }
        GD.Print($"Clicked hex {coord}: {tile.BiomeId}, tribe={tribe?.Name ?? "none"}");
    }
}
