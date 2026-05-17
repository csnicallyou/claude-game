using Godot;

namespace EpochsOfHumanity.UI.Menu;

/// <summary>
/// Main menu screen. Currently a stub — buttons mostly disabled until v0.1 features arrive.
/// </summary>
/// <remarks>
/// This is a render/UI script, so <c>using Godot</c> is allowed here (Law 2).
/// Do not import core sim logic from this file beyond what's exposed via the
/// upcoming <c>GameRoot</c> autoload.
/// </remarks>
public partial class MainMenu : Control
{
    public override void _Ready()
    {
        var newGameButton = GetNode<Button>("%NewGameButton");
        newGameButton.Pressed += OnNewGamePressed;

        var loadGameButton = GetNode<Button>("%LoadGameButton");
        loadGameButton.Pressed += OnLoadGamePressed;

        var settingsButton = GetNode<Button>("%SettingsButton");
        settingsButton.Pressed += OnSettingsPressed;

        var quitButton = GetNode<Button>("%QuitButton");
        quitButton.Pressed += OnQuitPressed;

        // Version label from project settings — keeps version single-source-of-truth
        var versionLabel = GetNode<Label>("%VersionLabel");
        var version = (string)ProjectSettings.GetSetting("application/config/version", "0.0.0");
        versionLabel.Text = TranslationServer.Translate("ui.version_label")
            .ToString()
            .Replace("{version}", version);
    }

    private void OnNewGamePressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/MapScreen.tscn");
    }

    private void OnLoadGamePressed()
    {
        GD.Print("Load Game pressed — not yet implemented.");
    }

    private void OnSettingsPressed()
    {
        GD.Print("Settings pressed — not yet implemented.");
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
