using Godot;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using EpochsOfHumanity.Core.Geography;
using EpochsOfHumanity.Core.Save;
using EpochsOfHumanity.Core.Time;
using EpochsOfHumanity.Core.Visual;
using EpochsOfHumanity.Sim.Biomes;
using EpochsOfHumanity.Sim.Characters;
using EpochsOfHumanity.Sim.Geography;
using EpochsOfHumanity.Sim.State;
using GodotVector2 = Godot.Vector2;
using NumericsVector2 = System.Numerics.Vector2;

namespace EpochsOfHumanity.Render.Map;

/// <summary>
/// Main strategic map screen — Levant region, real-time season-by-season time
/// advancement with 4 speeds, chief succession, save/load, seasonal palette shift.
/// </summary>
public partial class MapScreen : Node2D
{
    private const string QuickSaveFile = "user://saves/quick.save";

    /// <summary>Real seconds per game season at each speed. 1× = 10s/year = 2.5s/season.</summary>
    private static readonly Dictionary<GameSpeed, double> SecondsPerSeasonBySpeed = new()
    {
        [GameSpeed.Paused] = 0.0,
        [GameSpeed.Normal] = 2.5,
        [GameSpeed.Fast]   = 1.25,
        [GameSpeed.Faster] = 0.5,
    };

    /// <summary>
    /// Parchment paper-tint per season — subtle warm/cool palette shift.
    /// See game-visual-style skill, §"Климатический сдвиг".
    /// </summary>
    private static readonly Dictionary<Season, Color> PaperTintBySeason = new()
    {
        [Season.Spring] = new Color(0.97f, 0.93f, 0.78f, 1f),   // fresh, slightly green-warm
        [Season.Summer] = new Color(0.99f, 0.92f, 0.74f, 1f),   // golden, warmest
        [Season.Autumn] = new Color(0.92f, 0.83f, 0.67f, 1f),   // rust-tinged
        [Season.Winter] = new Color(0.91f, 0.92f, 0.88f, 1f),   // cool pale
    };

    private static readonly Dictionary<Season, string> SeasonName = new()
    {
        [Season.Spring] = "Spring",
        [Season.Summer] = "Summer",
        [Season.Autumn] = "Autumn",
        [Season.Winter] = "Winter",
    };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Export] public float MinZoom { get; set; } = 0.25f;
    [Export] public float MaxZoom { get; set; } = 4.0f;
    [Export] public float ZoomStep { get; set; } = 1.15f;

    private Camera2D? _camera;
    private HexMapRenderer? _renderer;
    private RiverRenderer? _riverRenderer;
    private LandmarkLabels? _landmarks;
    private Node2D? _tribesLayer;
    private ColorRect? _parchmentOverlay;
    private Label? _statusLabel;
    private Label? _yearLabel;
    private Label? _tribeLabel;
    private Button? _pauseButton;
    private Button? _speed1Button;
    private Button? _speed2Button;
    private Button? _speed3Button;
    private Button? _saveButton;
    private Button? _loadButton;
    private Panel? _notifPanel;
    private Label? _notifLabel;
    private Timer? _notifTimer;
    private Timer? _seasonTimer;

    private HexMap? _map;
    private TribeRegistry? _tribes;
    private PaletteRegistry? _palette;
    private HexLayout _layout = HexLayout.Default;

    private GameState? _state;
    private GameSpeed _speed = GameSpeed.Paused;

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
        _parchmentOverlay = GetNode<ColorRect>("OverlayLayer/ParchmentOverlay");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _yearLabel = GetNode<Label>("%YearLabel");
        _tribeLabel = GetNode<Label>("%TribeLabel");
        _pauseButton = GetNode<Button>("%PauseButton");
        _speed1Button = GetNode<Button>("%Speed1Button");
        _speed2Button = GetNode<Button>("%Speed2Button");
        _speed3Button = GetNode<Button>("%Speed3Button");
        _saveButton = GetNode<Button>("%SaveButton");
        _loadButton = GetNode<Button>("%LoadButton");
        _notifPanel = GetNode<Panel>("%NotifPanel");
        _notifLabel = GetNode<Label>("%NotifLabel");
        _notifTimer = GetNode<Timer>("%NotifTimer");
        _seasonTimer = GetNode<Timer>("%YearTimer"); // kept node name for backward-compat

        _pauseButton.Pressed += () => SetSpeed(GameSpeed.Paused);
        _speed1Button.Pressed += () => SetSpeed(GameSpeed.Normal);
        _speed2Button.Pressed += () => SetSpeed(GameSpeed.Fast);
        _speed3Button.Pressed += () => SetSpeed(GameSpeed.Faster);
        _saveButton.Pressed += OnSavePressed;
        _loadButton.Pressed += OnLoadPressed;

        _notifTimer.Timeout += HideNotification;
        _seasonTimer.Timeout += OnSeasonTimerTick;

        _notifPanel.Visible = false;

        try
        {
            LoadAndRender();
            CenterCameraOnStartingHex();
            SetSpeed(GameSpeed.Paused);
            ApplySeasonalPalette();
            RefreshHud();
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"MapScreen init failed: {ex.Message}\n{ex.StackTrace}");
            if (_statusLabel != null) _statusLabel.Text = $"ERROR: {ex.Message}";
        }
    }

    private void LoadAndRender()
    {
        var paletteJson = FileAccess.GetFileAsString("res://assets/palettes/paleolithic-base.json");
        var paletteDto = JsonSerializer.Deserialize<PaletteDto>(paletteJson, JsonOpts)
            ?? throw new System.IO.InvalidDataException("Palette JSON empty");
        _palette = new PaletteRegistry(paletteDto.Id, paletteDto.Colors);

        var biomes = LoadBiomes();
        var biomeRegistry = new BiomeRegistry(biomes);

        _map = LevantPreset.Build();
        _renderer!.Initialize(_map, biomeRegistry, _palette, _layout);

        _riverRenderer?.Initialize(LevantRivers.All(), _layout, _palette);
        _landmarks?.Initialize(_layout);

        _tribes = LevantTribesPreset.Build();
        _state = new GameState(seed: "levant-2026-default", initialTribes: _tribes);

        RenderTribes();
    }

    private static List<Biome> LoadBiomes()
    {
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

        var biomes = new List<Biome>();
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
        if (_tribesLayer == null || _tribes == null || _palette == null || _state == null) return;
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

    // ---------- Speed control ----------

    private void SetSpeed(GameSpeed speed)
    {
        _speed = speed;
        if (_seasonTimer == null) return;

        if (speed == GameSpeed.Paused)
        {
            _seasonTimer.Stop();
        }
        else
        {
            _seasonTimer.WaitTime = SecondsPerSeasonBySpeed[speed];
            _seasonTimer.Start();
        }

        UpdateSpeedButtonStyles();
        RefreshHud();
    }

    private void UpdateSpeedButtonStyles()
    {
        if (_pauseButton == null || _speed1Button == null || _speed2Button == null || _speed3Button == null) return;
        _pauseButton.Disabled  = _speed == GameSpeed.Paused;
        _speed1Button.Disabled = _speed == GameSpeed.Normal;
        _speed2Button.Disabled = _speed == GameSpeed.Fast;
        _speed3Button.Disabled = _speed == GameSpeed.Faster;
    }

    private void OnSeasonTimerTick()
    {
        if (_speed != GameSpeed.Paused) StepSeason();
    }

    private void StepSeason()
    {
        if (_state == null) return;
        _state.AdvanceSeason();

        // Notify on events affecting the player's tribe (only on year transition will there be any)
        foreach (var ev in _state.LatestEvents)
        {
            if (ev.TribeId == "sons-of-carmel")
            {
                ShowNotification(ev.Message);
                break;
            }
        }

        ApplySeasonalPalette();
        RefreshHud();
        UpdateSelectedHexStatus();
    }

    /// <summary>Push current-season paper-tint to the parchment shader.</summary>
    private void ApplySeasonalPalette()
    {
        if (_state == null || _parchmentOverlay == null) return;
        if (_parchmentOverlay.Material is not ShaderMaterial mat) return;

        var tint = PaperTintBySeason[_state.CurrentSeason];
        mat.SetShaderParameter("paper_tint", tint);
    }

    private void RefreshHud()
    {
        if (_state == null || _tribes == null) return;

        if (_yearLabel != null)
        {
            var speedLabel = _speed switch
            {
                GameSpeed.Paused => "⏸",
                GameSpeed.Normal => "▶",
                GameSpeed.Fast   => "▶▶",
                GameSpeed.Faster => "▶▶▶",
                _                => "?",
            };
            _yearLabel.Text = $"{_state.CurrentYearBP:N0} BP · {SeasonName[_state.CurrentSeason]}  {speedLabel}";
        }

        if (_tribeLabel != null)
        {
            var player = _tribes.Player;
            var chief = _state.ChiefOf(player.Id);
            var sex = chief.Sex == Sex.Male ? "♂" : "♀";
            _tribeLabel.Text = $"{player.Name} · chief {chief.Name} {sex} {chief.AgeWinters} winters";
        }

        if (_statusLabel != null && _map != null)
        {
            _statusLabel.Text =
                $"Levant — {_map.Count} hexes  |  " +
                $"{_tribes.Count - 1} neighbouring tribes  |  " +
                $"year {_state.CurrentYearBP:N0} BP · {SeasonName[_state.CurrentSeason]}  " +
                $"({_state.YearsElapsed} winters lived)";
        }
    }

    // ---------- Save / Load ----------

    private void OnSavePressed()
    {
        if (_state == null) return;
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("user://saves/"));
            var snapshot = SaveStore.ToSnapshot(_state, "quick");
            var bytes = SaveSerializer.Serialize(snapshot);
            using var file = FileAccess.Open(QuickSaveFile, FileAccess.ModeFlags.Write);
            if (file == null) throw new System.IO.IOException("Cannot open save file for writing");
            file.StoreBuffer(bytes);
            ShowNotification($"Saved at year {_state.CurrentYearBP:N0} BP, {SeasonName[_state.CurrentSeason]}.");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"Save failed: {ex}");
            ShowNotification($"Save failed: {ex.Message}");
        }
    }

    private void OnLoadPressed()
    {
        if (_tribes == null) return;
        try
        {
            if (!FileAccess.FileExists(QuickSaveFile))
            {
                ShowNotification("No save file yet — press Save first.");
                return;
            }
            using var file = FileAccess.Open(QuickSaveFile, FileAccess.ModeFlags.Read);
            if (file == null) throw new System.IO.IOException("Cannot open save file for reading");
            var bytes = file.GetBuffer((long)file.GetLength());
            var snapshot = SaveSerializer.Deserialize(bytes);
            _state = SaveStore.FromSnapshot(snapshot, _tribes);
            RenderTribes();
            SetSpeed(GameSpeed.Paused);
            ApplySeasonalPalette();
            RefreshHud();
            UpdateSelectedHexStatus();
            ShowNotification($"Loaded — year {_state.CurrentYearBP:N0} BP, {SeasonName[_state.CurrentSeason]}.");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"Load failed: {ex}");
            ShowNotification($"Load failed: {ex.Message}");
        }
    }

    // ---------- Notification popup ----------

    private void ShowNotification(string text)
    {
        if (_notifPanel == null || _notifLabel == null || _notifTimer == null) return;
        _notifLabel.Text = text;
        _notifPanel.Visible = true;
        _notifTimer.Start(4.0);
    }

    private void HideNotification()
    {
        if (_notifPanel != null) _notifPanel.Visible = false;
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
            else _isPanning = false;
        }
        else if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed) ZoomBy(ZoomStep);
        else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed) ZoomBy(1.0f / ZoomStep);
        else if (mb.ButtonIndex == MouseButton.Left && mb.Pressed) OnHexClicked();
    }

    private void HandleMousePan(InputEventMouseMotion mm)
    {
        if (_camera == null) return;
        var delta = mm.GlobalPosition - _panStartMouse;
        _camera.Position = _panStartCamera - delta / _camera.Zoom.X;
    }

    private void HandleKeyPress(InputEventKey ke)
    {
        switch (ke.Keycode)
        {
            case Key.Escape:
                GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
                break;
            case Key.Space:
                SetSpeed(_speed == GameSpeed.Paused ? GameSpeed.Normal : GameSpeed.Paused);
                break;
            case Key.Key1: SetSpeed(GameSpeed.Normal); break;
            case Key.Key2: SetSpeed(GameSpeed.Fast); break;
            case Key.Key3: SetSpeed(GameSpeed.Faster); break;
            case Key.Period:
            case Key.Enter:
            case Key.KpEnter:
                if (_speed == GameSpeed.Paused) StepSeason();
                break;
            case Key.S when ke.CtrlPressed: OnSavePressed(); break;
            case Key.L when ke.CtrlPressed: OnLoadPressed(); break;
        }
    }

    private void ZoomBy(float factor)
    {
        if (_camera == null) return;
        var z = Mathf.Clamp(_camera.Zoom.X * factor, MinZoom, MaxZoom);
        _camera.Zoom = new GodotVector2(z, z);
    }

    private void OnHexClicked()
    {
        if (_camera == null || _map == null) return;
        var worldPos = _camera.GetGlobalMousePosition();
        _selectedHex = _layout.PixelToHex(new NumericsVector2(worldPos.X, worldPos.Y));
        UpdateSelectedHexStatus();
    }

    private void UpdateSelectedHexStatus()
    {
        if (_statusLabel == null || _map == null || _selectedHex == null || _state == null) return;
        var coord = _selectedHex.Value;

        if (!_map.TryGet(coord, out var tile))
        {
            _statusLabel.Text = $"Hex {coord} — (outside Levant)  |  year {_state.CurrentYearBP:N0} BP · {SeasonName[_state.CurrentSeason]}";
            return;
        }

        var tribe = _tribes?.AtHex(coord);
        if (tribe != null)
        {
            var chief = _state.ChiefOf(tribe.Id);
            var speciesLabel = tribe.Species == Species.Sapiens ? "Sapiens"
                : tribe.Species == Species.Neanderthal ? "Neanderthal" : "Denisovan";
            var marker = tribe.IsPlayerControlled ? " ★" : "";
            var sex = chief.Sex == Sex.Male ? "♂" : "♀";
            _statusLabel.Text =
                $"Hex {coord} — {tribe.Name} ({speciesLabel}){marker}  |  " +
                $"chief {chief.Name} {sex} {chief.AgeWinters} winters  |  " +
                $"biome: {tile.BiomeId}  |  {_state.CurrentYearBP:N0} BP · {SeasonName[_state.CurrentSeason]}";
        }
        else
        {
            _statusLabel.Text =
                $"Hex {coord} — biome: {tile.BiomeId}  |  {_state.CurrentYearBP:N0} BP · {SeasonName[_state.CurrentSeason]}";
        }
    }
}

public enum GameSpeed
{
    Paused = 0,
    Normal = 1,
    Fast   = 2,
    Faster = 3,
}
