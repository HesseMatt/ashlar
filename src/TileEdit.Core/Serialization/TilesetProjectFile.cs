using System.Text.Json;

namespace TileEdit.Core.Serialization;

public static class TilesetProjectFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static void Save(TilesetProject project, string filePath)
    {
        var json = JsonSerializer.Serialize(project, Options);
        File.WriteAllText(filePath, json);
    }

    public static TilesetProject Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<TilesetProject>(json, Options)
            ?? throw new InvalidDataException($"Project file '{filePath}' is empty or invalid.");
    }
}
