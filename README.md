# tileedit

Lightweight, local desktop application for managing tilesets for game
development — primarily for use with **MonoGame** / `MonoGame.Extended`.

## Features

### Import and export

- Import a tileset image and auto-detect the tile size, with confirmation
  or override before the grid is finalized.
- Tiles are shown as a grid on a transparency checkerboard; fully
  transparent cells are skipped on import.
- Every tile gets a unique, stable name that doesn't change when the tile
  is moved.
- Export the edited image together with metadata XML in the
  `MonoGame.Extended` `TextureAtlas` format (see
  [`example.xml`](example.xml)).

### Editing

- Tool palette with Select, Rectangle select, and Move tools.
- Delete and move tiles, including the underlying image pixels (moving
  onto an occupied cell overwrites it).
- Assign tiles to named groups; their names are renamed to use the group
  as prefix.
- Compact the atlas to remove gaps and shrink the canvas.
- Undo and redo.
- Zoom the canvas.

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
