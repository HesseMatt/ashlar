using TileEdit.Core.Model;

namespace TileEdit.Core.Serialization;

// Self-contained, like a .psd: the image itself is embedded (PNG-encoded),
// so the project file keeps working even if the original image is moved,
// renamed or deleted. ImageFileName is kept only as the suggested name when
// exporting the image again, not as a dependency used to load the project.
// DrawColor is optional so project files from before the Draw tool still load.
public record TilesetProject(string ImageFileName, byte[] ImagePng, string TexturePath, int TileWidth, int TileHeight, IReadOnlyList<TileRegion> Tiles, PixelColor? DrawColor = null);
