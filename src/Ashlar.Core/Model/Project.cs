namespace Ashlar.Core.Model;

public class Project
{
    public IReadOnlyList<ProjectTileset> Tilesets { get; }

    public IReadOnlyList<ProjectMap> Maps { get; }

    public Project(IReadOnlyList<ProjectTileset> tilesets, IReadOnlyList<ProjectMap> maps)
    {
        var duplicate = tilesets.GroupBy(tileset => tileset.Name).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Tileset name '{duplicate.Key}' is used more than once.", nameof(tilesets));

        Tilesets = tilesets;
        Maps = maps;
    }
}
