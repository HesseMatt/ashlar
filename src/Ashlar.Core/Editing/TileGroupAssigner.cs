using System.Text.RegularExpressions;
using Ashlar.Core.Model;

namespace Ashlar.Core.Editing;

// A tile's group is not a separate field - it's encoded as the prefix of its
// name (e.g. "enemies_0003"). The default, ungrouped state is the
// {tilesetname}_{nnnn} scheme produced by TilesetGridBuilder.
public static class TileGroupAssigner
{
    private static readonly Regex NamePattern = new(@"^(?<prefix>.+)_(?<number>\d+)$");

    public static string FormatName(string prefix, int number) => $"{prefix}_{number:D4}";

    // Turns the group combo box's raw text into the prefix to assign: blank
    // or an entirely invalid (sanitizes to nothing) entry both mean "no
    // group", i.e. the default naming scheme.
    public static string ResolveTargetGroupName(string enteredName, string defaultGroupName)
    {
        var trimmed = enteredName.Trim();
        if (trimmed.Length == 0)
            return defaultGroupName;

        var sanitized = TilesetGridBuilder.SanitizeBaseName(trimmed);
        return sanitized.Length == 0 ? defaultGroupName : sanitized;
    }

    public static string? GetGroupName(TileRegion tile, string defaultGroupName)
    {
        var prefix = Parse(tile.Name)?.Prefix;
        return prefix == defaultGroupName ? null : prefix;
    }

    public static IReadOnlyList<string> GetGroupNames(IReadOnlyList<TileRegion> tiles, string defaultGroupName)
    {
        var seen = new HashSet<string>();
        var names = new List<string>();

        foreach (var tile in tiles)
        {
            var prefix = Parse(tile.Name)?.Prefix;
            if (prefix is not null && prefix != defaultGroupName && seen.Add(prefix))
                names.Add(prefix);
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    // Renumbers each selected tile to one greater than the highest number
    // already used in the target group, in ascending order of the tile's
    // position in `tiles` - never simply the member count, so a gap left by
    // a tile that was removed from the group is not reused.
    public static IReadOnlyList<TileRegion> AssignGroup(IReadOnlyList<TileRegion> tiles, IReadOnlyCollection<TileRegion> selectedTiles, string groupName)
    {
        var selected = new HashSet<TileRegion>(selectedTiles);
        var nextNumber = HighestNumber(tiles, selected, groupName) + 1;

        var result = new List<TileRegion>(tiles.Count);
        foreach (var tile in tiles)
        {
            if (!selected.Contains(tile))
            {
                result.Add(tile);
                continue;
            }

            result.Add(tile with { Name = FormatName(groupName, nextNumber) });
            nextNumber++;
        }

        return result;
    }

    public static int HighestNumber(IReadOnlyList<TileRegion> tiles, string prefix) =>
        HighestNumber(tiles, new HashSet<TileRegion>(), prefix);

    private static int HighestNumber(IReadOnlyList<TileRegion> tiles, IReadOnlySet<TileRegion> excluded, string prefix)
    {
        var highest = 0;

        foreach (var tile in tiles)
        {
            if (excluded.Contains(tile))
                continue;

            var parsed = Parse(tile.Name);
            if (parsed is { } p && p.Prefix == prefix)
                highest = Math.Max(highest, p.Number);
        }

        return highest;
    }

    private readonly record struct ParsedName(string Prefix, int Number);

    private static ParsedName? Parse(string name)
    {
        var match = NamePattern.Match(name);
        return match.Success ? new ParsedName(match.Groups["prefix"].Value, int.Parse(match.Groups["number"].Value)) : null;
    }
}
