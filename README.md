# tileedit

Lightweight, local desktop application for managing tilesets for game
development — primarily for use with **MonoGame** / `MonoGame.Extended`.

> **Status:** v1 complete; tile editing (delete/move) in progress.

## Feature scope (v1)

- Open a tileset image, auto-detect tile size (with confirmation),
  display tiles visually as a grid.
- Generate metadata XML in the `MonoGame.Extended` `TextureAtlas` format
  (see [`example.xml`](example.xml)), including unique, stable tile
  names.

## Planned (in progress)

- Delete and move individual tiles, including adjusting the underlying
  image file.
- A project file to resume editing a tileset later — and, for a
  tileset that was only ever exported before this existed, resuming
  straight from that existing export XML instead of starting over.

Further out: tile grouping, atlas compacting.

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
