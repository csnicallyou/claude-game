using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EpochsOfHumanity.Core.DataLoader;

/// <summary>
/// Loads JSON configs from <c>data/</c> directory tree.
/// </summary>
/// <remarks>
/// Engine-agnostic — uses <see cref="System.IO.File"/> directly, not Godot's FileAccess.
/// The render layer (or main game bootstrap) is responsible for resolving the actual
/// path on disk (Godot.OS.GetUserDataDir() etc).
///
/// Files in a directory are loaded in **filename-sorted order** for determinism (Law 1).
/// Mod overlay (merging mod files on top of vanilla) is handled by <c>game-modding</c>
/// at a higher level — this class only reads vanilla.
/// </remarks>
public sealed class JsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _dataRoot;

    public JsonLoader(string dataRoot)
    {
        _dataRoot = dataRoot ?? throw new System.ArgumentNullException(nameof(dataRoot));
    }

    /// <summary>
    /// Loads all *.json files from <paramref name="relativeDir"/>, deserializing each as <typeparamref name="T"/>.
    /// Files are loaded in filename-sorted order (Ordinal).
    /// </summary>
    public List<T> LoadAll<T>(string relativeDir) where T : class
    {
        var fullPath = Path.Combine(_dataRoot, relativeDir);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Data directory not found: {fullPath}");

        var files = Directory.GetFiles(fullPath, "*.json", SearchOption.TopDirectoryOnly);
        System.Array.Sort(files, System.StringComparer.Ordinal); // determinism

        var results = new List<T>(files.Length);
        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            try
            {
                var entry = JsonSerializer.Deserialize<T>(json, Options)
                    ?? throw new InvalidDataException($"Empty JSON in {file}");
                results.Add(entry);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Failed to parse {file}: {ex.Message}", ex);
            }
        }
        return results;
    }

    /// <summary>Load a single named JSON file.</summary>
    public T LoadOne<T>(string relativePath) where T : class
    {
        var fullPath = Path.Combine(_dataRoot, relativePath);
        var json = File.ReadAllText(fullPath);
        return JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new InvalidDataException($"Empty JSON in {fullPath}");
    }
}
