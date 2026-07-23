# Castle Breach — Unity project

A 2D tower-defense / action-defense hybrid built in Unity, from the
**Castle Breach Design Document v1.0**. Standalone repo, entirely independent
of any other project.

## Layout

| Path | What it is |
|---|---|
| `guides/` | Ordered, click-by-click beginner guides (00 → 05). **Start at `guides/00-unity-setup.md`.** |
| `unity-scripts/` | The vertical slice's C# code, staged for import. Guide 00 moves it into the Unity project (`CastleBreach/Assets/Scripts`), after which this folder is deleted — the code's permanent home is inside the project. |
| `CastleBreach/` | The Unity project itself. Created on the designer's machine by Unity Hub in Guide 00 (Unity 6 LTS, Universal 2D template), then committed. `.gitignore` here already excludes `Library/`, `Temp/`, logs, and IDE files. |

## Status

- [x] Design doc v1.0 finalized (kept outside the repo; a summary of every
      implemented rule is in the guides and script comments, each tagged with
      its doc section, e.g. `§7.3`)
- [x] Vertical-slice code written (16 scripts: map generator, player, king,
      zombie, waves, currency, archer tower, build mode, camera, HUD)
- [ ] Guide 00 — Unity installed, project created, scripts imported
- [ ] Guide 01 — castle map (grid, walls, gates)
- [ ] Guide 02 — player & camera
- [ ] Guide 03 — King & zombies
- [ ] Guide 04 — currency & HUD
- [ ] Guide 05 — archer tower & build mode → **vertical slice complete**

(Tick these off in commits as you go, so any future session knows where things
stand.)

## Deferred — noted for later

- **Hand-building/editing the map directly** (not just via coordinate-list
  regions in the Inspector). Right now `CastleMapGenerator` only takes typed
  doc coordinates (e.g. `B14`–`B17`); there's no click-to-paint tile editing.
  The user wants this eventually — likely folds into the **Map Builder tool**
  (design doc §3.5/§10.5) rather than being its own thing. Revisit when that
  gets built.
- **Gates showing open/closed state visually** (e.g. opacity change). Not in
  the vertical slice yet. When built: this is a *runtime* change, not
  Editor-authored state, so it's unaffected by the tile-coloring rule below —
  either swap between two pre-made Tile assets (`GateOpenTile`/
  `GateClosedTile`, each with its own baked color/alpha) via
  `tilemap.SetTile(cell, asset)`, or call `tilemap.SetColor(cell, color)`
  live from gameplay code. Both are fine; only Editor-time-only color writes
  were the problem (see below).
- **Different ground/wall/gate colors per campaign level** (e.g. a snow or
  ruins theme). Already supported by the current architecture with zero code
  changes: make new Tile assets per level (e.g. `GroundTile_Snow`) with their
  own baked-in color, and assign them to that level's `MapGenerator` Ground/
  Wall/Gate Tile fields instead. One `CastleMapGenerator` instance per level/
  scene, each pointing at its own themed tile set.
- **Changing the map size** (larger or smaller than 40×30). This one *does*
  need a code change, unlike the two above — `Columns`/`Rows` are
  `public const int` in `GridMath.cs`, compile-time constants shared globally
  by everything that touches grid coordinates (`CastleMapGenerator`,
  `TileRef`'s bounds-checking, `WaveSpawner`'s fallback spawn point,
  `BuildModeController`'s placement bounds check). Two different asks here,
  worth telling apart:
  - **Resize this one map** (still just a single fixed size): edit the two
    constants in `GridMath.cs`, then update `CastleMapGenerator`'s wall/gate
    region coordinates (`A1`–`B30` etc. are hand-typed to the current 40×30
    bounds per doc §3.2/§3.3), the King/Player spawn positions, and the Main
    Camera's starting position/size from Guide 01/02 — all manual but
    mechanical.
  - **Different grid sizes per campaign level** (design doc §3.4 explicitly
    anticipates "grid size" varying by level) — a real refactor: `Columns`/
    `Rows` would need to stop being global `const`s and become per-level data
    instead (e.g. fields on `CastleMapGenerator`, with `GridMath`'s methods
    taking columns/rows as parameters rather than reading static consts).
    Worth doing once actual campaign levels/the Map Builder are being built,
    not speculatively before then.

## Conventions (for future work — human or Claude)

- **Grid:** 40×30 tiles, 1 tile = 1 world unit, map's bottom-left at world
  (0,0). Doc coordinates ("A1".."AN30", row 1 at the top) are parsed by
  `TileRef`; all conversions go through `GridMath`. Never hand-convert.
- **Layers:** 6 Player, 7 Enemy, 8 Structure, 9 King.
- **Sprite sort orders:** map 0–2, coins 15, structures 19, characters 20–21,
  projectiles 25, health bars 30–31, placement ghost 40.
- **Placeholder policy (doc §1):** every visual is a tinted white Square/Circle
  sprite; sizes, timings and stats are Inspector fields — swapping in real art
  or tuning numbers must never require code changes.
- **Tile colors live on the Tile asset itself** (`GroundTile`/`WallTile`/
  `GateTile`'s own `Color` field), not on `CastleMapGenerator` or applied
  per-cell via script. Reason (hard-won): `Tilemap.SetTile()` applies the
  placed tile's own default flags to that cell, including `LockColor` (the
  default on tiles created via the Tile Palette); a script-driven per-cell
  `SetColor()` override made outside Play Mode looked correct in the Editor
  but didn't reliably survive into a fresh Play session, reverting to the
  tile's locked default. **The general rule:** anything that needs to exist
  as pre-authored starting state belongs on an asset or Inspector field, not
  written by an Editor-time script call — but changing a Tilemap's color
  live, *during* actual gameplay, is completely fine and unaffected by this.
  Non-Tilemap placeholders (player, towers, etc.) still just use a normal
  `SpriteRenderer.color` field, which was never affected either.
- **Stats carry doc-section comments** (`§6`, `§7.3`, …) so numbers can be
  traced back to the design doc.
- The design doc's roadmap after the slice is summarized at the end of
  `guides/05-archer-tower-and-build-mode.md`.
