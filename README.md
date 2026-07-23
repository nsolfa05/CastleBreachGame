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
- [x] Guide 00 — Unity installed, project created, scripts imported
- [x] Guide 01 — castle map (grid, walls, gates)
- [x] Guide 02 — player & camera
- [x] Guide 03 — King & zombies
- [x] Guide 04 — currency & HUD
- [x] Guide 05 — archer tower & build mode → **vertical slice complete**
- [x] Guide 06 — tower range circle
- [ ] Guide 07 — Phase 1: monster stats as ScriptableObjects (code pushed)
- [ ] Guide 08 — Phase 2: full monster roster (code pushed)
- [ ] Guide 09 — Phase 3: pike / catapult / praise-the-king towers (code pushed)

**Next up:** the full build order is in [`ROADMAP.md`](ROADMAP.md). Phases
1–3 are code-complete — work through guides 07 → 08 → 09, then ask Claude
for Phase 4 (walls, gates, pathfinding, tile weight).

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
- **Tile Weight Rule (design doc §7.1)** — monsters should stack up to a
  combined weight of 6 per tile (most monsters weigh 2, Cyclops weighs 6/
  fills the tile alone) and get pushed aside past that cap, rather than
  overlapping or physically colliding; the player always counts as weight 6,
  so nothing can ever share the player's tile. Not implemented yet — the
  vertical slice's zombies just bump off each other via ordinary
  Rigidbody2D/Collider2D physics, which Guide 03's "disable Enemy × Enemy
  collision" tip works around cosmetically but doesn't actually replace.
  Natural time to build this for real: once more monster types with varying
  weights exist (§7.3), since a single monster type can't really exercise
  the stacking cap.
- **Per-level map data — grid size, King spawn position, and immovable
  obstacles (rocks).** Design doc §3.4 explicitly groups these three
  together: *"Later maps will vary: king spawn position, grid size, starting
  walls/buildings, and immovable obstacles (e.g., rocks)."* Worth designing
  as one coherent thing when the time comes, not three separate patches —
  notes on each:
  - **Grid size** — the one with a real code cost. `Columns`/`Rows` are
    `public const int` in `GridMath.cs`, compile-time constants shared
    globally by everything touching grid coordinates
    (`CastleMapGenerator`, `TileRef`'s bounds-checking, `WaveSpawner`'s
    fallback spawn point, `BuildModeController`'s placement bounds check). A
    one-off resize of *this* map is just editing those two constants plus
    updating the hand-typed wall/gate region coordinates, King/Player spawn
    positions, and Main Camera framing to match. *Per-level* varying sizes
    is a bigger refactor: `Columns`/`Rows` would need to become per-level
    instance data (e.g. fields on `CastleMapGenerator`) instead of global
    consts, with `GridMath`'s methods taking them as parameters.
  - **King spawn position** — currently just the King GameObject's hand-set
    Transform position (Guide 03: `20, 15, 0`, map center). If each campaign
    level ends up as its own Scene, this already works with zero code
    changes — just place the King wherever that level needs. If levels
    instead load from shared data (matching the Map Builder plan in §10.5),
    King spawn position should become a field in that level data, with
    whatever loads the level moving the King there at runtime.
  - **Immovable obstacles (rocks)** — mechanically simple, since it's the
    same trick walls already use: a Tilemap Collider 2D physically blocks
    both the player and monsters via ordinary Rigidbody2D collision (there's
    no pathfinding yet — monsters move in a straight line per `ZombieAI` and
    just get physically stopped by anything solid, same as they are by
    walls today). Difference from walls: rocks are permanent, unbreakable
    level terrain with no HP, not a player-purchased structure (§6). Cleanest
    fit: its own Tilemap layer + Tile asset + an `obstacleRegions` list on
    `CastleMapGenerator`, mirroring the existing `wallRegions`/`gateRegions`
    pattern exactly.

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
