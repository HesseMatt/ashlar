# tileedit

Lightweight, local desktop application for managing tilesets for game
development. Primary target format is **MonoGame** (specifically
`MonoGame.Extended`'s `TextureAtlas` XML format).

## Goal / Scope

- Open a tileset image, auto-detect tile size, display the tiles visually
  as a grid.
- Generate metadata XML (MonoGame.Extended TextureAtlas format, see
  [`example.xml`](example.xml)) from the detected grid.
- Each tile gets a unique, stable name.
- Later: delete/move individual tiles, including adjusting the underlying
  image file.
- No server, no hosting, no cloud integration — a purely local desktop
  app.

### Non-goals (for now)

- No support for other engines/formats (e.g. Tiled TMX, Godot TileSet) —
  could be added later as an additional exporter, but is not a goal for
  v1/v2.
- No automatic atlas repacking/compacting in v1/v2 (its own, later
  phase).
- No undo/redo stack in v1 (can be added in v2 if destructive image
  editing turns out to be risky in practice).

## Tech stack

- **.NET 8** (LTS), C#.
- **Avalonia UI** for the desktop interface. Rationale: the only
  seriously mature native C#/XAML UI framework that actually runs
  natively (no Wine/VM) on Linux — WPF is Windows-only, .NET MAUI has
  weak desktop/Linux support. Avalonia + Fluent theme gives a modern
  look.
- **CommunityToolkit.Mvvm** for MVVM (view models, commands, observable
  properties) — cuts down boilerplate compared to hand-rolled MVVM.
- **SixLabors.ImageSharp** for image I/O and pixel manipulation (loading,
  clearing a region, copying pixel blocks). Pure managed code,
  cross-platform, much more ergonomic for pixel edits than raw
  Skia/Avalonia bitmap APIs. License note: Six Labors Split License —
  free to use for this (non-commercial/open-source) project; check the
  license again if this is ever used commercially.
- **xUnit** for unit tests of the core logic.

## Architecture

```
tileedit/
├── src/
│   ├── TileEdit.Core/       Domain logic, UI-independent
│   │   ├── Model/           Tileset, TileRegion, ...
│   │   ├── Detection/        Tile size detection
│   │   ├── Serialization/    Export XML (MonoGame.Extended format) + internal project format
│   │   └── Editing/          Delete/move/compact operations on image + metadata
│   └── TileEdit.App/        Avalonia application (views, view models)
├── tests/
│   └── TileEdit.Core.Tests/  xUnit tests for TileEdit.Core
├── TileEdit.sln
└── CLAUDE.md, README.md, LICENSE, .gitignore
```

`TileEdit.Core` has no dependency on Avalonia and is independently
testable. `TileEdit.App` only contains UI/bindings and calls into
`Core`.

## Data model & file formats

There are two distinct, deliberately separated file formats:

1. **Export XML** (`*.atlas.xml` or similar, MonoGame.Extended
   `TextureAtlas` format): exactly the format from
   [`example.xml`](example.xml) —
   `<TextureAtlas><Texture>...</Texture><Regions><Region name="" x="" y=""
   width="" height="" /></Regions></TextureAtlas>`. This is the
   game-ready artifact consumed by MonoGame.Extended. The schema stays
   strictly minimal (only `name`, `x`, `y`, `width`, `height`) to remain
   compatible — **do not** add extra attributes for groups etc.
2. **Project file** (internal working format, e.g. `*.tileproj.json`):
   stores the editing state (source image path, detected/adjusted tile
   size, current tile list with IDs and positions, eventually group
   assignment). Needed starting Phase 2 (editing features), because
   after deleting/moving tiles, the tile layout can no longer be
   reconstructed from the raw image dimensions alone. **Not version
   controlled, but also not "AI info"** — it's a normal application
   project file, comparable to a `.psd` or `.aseprite` file.

### Tile naming convention

- Unique, **stable** ID per tile, independent of its position in the
  grid. Moving a tile does **not** change its name.
- Initial: `{tilesetname}_{nnnn}` (e.g. `dungeon_0001`), assigned
  sequentially when generating from the detected grid.
- Planned extension (later, not implemented in code for v1, but worth
  keeping in mind for the data model): the user selects multiple tiles
  and assigns them to a named group. Their IDs are then renamed to
  `{groupname}_{nnnn}`. This is purely a different choice of ID prefix
  plus its own counter per group — **no** schema change to the export
  XML is needed, since the group name is implicitly encoded in the
  `name` attribute.

### Tile size detection

- Computes candidates from the common divisors of image width/height
  (optionally weighted against common tile sizes) and suggests the most
  plausible value.
- The user **confirms or overrides** the suggestion before the grid is
  finalized — no silent, uncorrectable auto-detection.

## Phases / roadmap

- **Phase 1 (v1, initial version):**
  - Open a tileset image, detect tile size (with user confirmation),
    display the grid visually.
  - Generate export XML (MonoGame.Extended format) from the grid; tiles
    get stable IDs per the scheme above.
  - No persistence needed, since it's a pure derivation from the image.
- **Phase 2:**
  - Introduce the project file format (`*.tileproj.json`) as the basis
    for editing.
  - Delete tiles (remove the metadata entry, clear/transparent the pixel
    region in the image).
  - Move tiles (copy the pixel block into the new grid cell, clear the
    old cell, update metadata). Atlas canvas size stays fixed.
- **Phase 3 (later):**
  - Group assignment for tiles (UI + renaming by group prefix).
  - "Compact" function: repack the atlas/shrink the canvas, remove gaps.

## Development approach

- **Everything — code, comments, docs, commit messages, UI strings — is
  written in English**, even though we discuss the project in German in
  chat.
- Work proceeds **feature by feature, not all at once**. Each step in
  the implementation plan is small enough to review and manually test
  before moving on to the next one. Don't start the next step until the
  current one has been reviewed.
- Code should be **readable and look like it was written by a human**:
  clear names, straightforward control flow, no AI-generated-looking
  over-abstraction, no defensive code for cases that can't happen. The
  point isn't just a working app — it's code the user can read and
  learn from.
- No comments that just describe *what* the code does — only when the
  *why* isn't obvious (e.g. workarounds, non-trivial invariants).
- Core logic stays UI-independent and is tested with xUnit; the UI layer
  (`TileEdit.App`) contains no business logic.
- No abstractions/configuration options beyond what one of the phases
  above actually needs.

### Commits

- **The user commits, not Claude.** Claude prepares/stages changes and
  proposes a commit message, but runs `git commit` only if explicitly
  asked to in the moment — the default is to leave changes ready for
  the user to review and commit themselves. No `Co-Authored-By` trailer
  for Claude.
- Commit message style: English, imperative subject line (e.g. "Add
  tile size detector", not "Added" or "Adding"), with a short body
  explaining *why* the change was made, not a restatement of the diff.
  Skip the body only for truly trivial changes.
- No GitHub Issues/Projects for this repo — tracking work items that way
  is intentionally out of scope; the implementation plan under
  `.planning/` and plain commits are enough.

## Build & run

(To be filled in once the solution skeleton exists.)

```bash
dotnet build
dotnet run --project src/TileEdit.App
dotnet test
```

## Not version controlled

Implementation plans, session notes, and other AI working material live
under `.planning/` and are excluded via `.gitignore`. This file
(`CLAUDE.md`) itself is part of the repo, since it serves as the
long-lived project description for both humans and AI tooling.
