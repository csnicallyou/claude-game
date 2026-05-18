using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EpochsOfHumanity.Core.Save;

/// <summary>
/// Serialize/deserialize <see cref="GameSaveState"/> to/from gzipped JSON bytes.
/// </summary>
/// <remarks>
/// Pattern: Paradox-style. JSON for transparency (modders can decompress and
/// inspect), gzip for size + speed. Engine-agnostic (Law 2).
/// </remarks>
public static class SaveSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IncludeFields = false,
        PropertyNameCaseInsensitive = true,
    };

    public static byte[] Serialize(GameSaveState state)
    {
        var json = JsonSerializer.Serialize(state, Options);
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            gz.Write(bytes, 0, bytes.Length);
        }
        return ms.ToArray();
    }

    public static GameSaveState Deserialize(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gz, Encoding.UTF8);
        var json = reader.ReadToEnd();
        var state = JsonSerializer.Deserialize<GameSaveState>(json, Options)
            ?? throw new InvalidDataException("Save deserialized to null");

        if (state.FormatVersion > GameSaveState.CurrentFormatVersion)
            throw new InvalidDataException(
                $"Save is from a newer game version (format v{state.FormatVersion}; current v{GameSaveState.CurrentFormatVersion}).");
        if (state.FormatVersion < GameSaveState.CurrentFormatVersion)
            throw new InvalidDataException(
                $"Save is from an older format (v{state.FormatVersion}); migrations land in v0.5 of the game.");

        return state;
    }
}
