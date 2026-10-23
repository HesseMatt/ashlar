namespace Ashlar.Core.Model;

public record Tileset(string TexturePath, int TileWidth, int TileHeight, IReadOnlyList<TileRegion> Tiles);
