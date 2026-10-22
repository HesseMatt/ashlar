# tileedit

Lightweight, local desktop application for managing tilesets for game
development.

## Features

### Import and export

Import a tileset image and export the edited image together with an XML
file describing every tile by name and position:

```xml
<?xml version="1.0" encoding="utf-8"?>
<TextureAtlas>
    <Texture>dungeon</Texture>
    <Regions>
        <Region name="dungeon_0001" x="0" y="0" width="16" height="16" />
        <Region name="dungeon_0002" x="16" y="0" width="16" height="16" />
        <Region name="dungeon_0003" x="32" y="0" width="16" height="16" />
    </Regions>
</TextureAtlas>
```

### Editing

- Select, rectangle select, and move tools
- Delete tiles
- Draw tool for painting single pixels with an RGBA color
- Tile groups
- Undo and redo
- Zoom

### Project files

- Save and reopen a tileset as a self-contained project file
  (`*.tileproj.json`, embeds the image) to resume editing later.
- Images are only ever imported to start a new tileset; the export XML is
  an output and cannot be reopened.

## Tech stack

.NET 8, Avalonia UI (cross-platform: Linux/Windows/macOS), ImageSharp.

## Build & run

```bash
dotnet build
dotnet run --project src/TileEdit.App
```

## License

[PolyForm Noncommercial 1.0.0](LICENSE). In short: free to use, copy,
modify, and distribute for any noncommercial purpose, as long as the
required copyright notice stays attached — **no commercial use or
resale**. There's no warranty of any kind.

Third-party notices (bundled icon assets): see [NOTICE](NOTICE).
