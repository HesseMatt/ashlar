using System.Text.Json;
using Ashlar.Core.Model;

namespace Ashlar.Core.Serialization;

public static class ProjectFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static void Save(Project project, string filePath)
    {
        var json = JsonSerializer.Serialize(project, Options);
        File.WriteAllText(filePath, json);
    }

    public static Project Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<Project>(json, Options)
            ?? throw new InvalidDataException($"Project file '{filePath}' is empty or invalid.");
    }
}
