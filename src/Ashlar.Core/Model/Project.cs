namespace Ashlar.Core.Model;

public class Project
{
    public IReadOnlyList<ProjectTileset> Tilesets { get; }

    public IReadOnlyList<TileMap> Maps { get; }

    public Project(IReadOnlyList<ProjectTileset> tilesets, IReadOnlyList<TileMap> maps)
    {
        var duplicate = tilesets.GroupBy(tileset => tileset.Name).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Tileset name '{duplicate.Key}' is used more than once.", nameof(tilesets));

        var duplicateMap = maps.GroupBy(map => map.Name).FirstOrDefault(group => group.Count() > 1);
        if (duplicateMap is not null)
            throw new ArgumentException($"Map name '{duplicateMap.Key}' is used more than once.", nameof(maps));

        Tilesets = tilesets;
        Maps = maps;
    }
}
