# Castle Breach — Unity project

A 2D tower-defense / action-defense hybrid built in Unity, from the
**Castle Breach Design Document v1.0**. Standalone repo, entirely independent
of any other project.

## Layout

| Path | What it is |
|---|---|
| `guides/` | Ordered, click-by-click beginner guides (00 → 09, plus 9.5 for playtesting-driven edits). **Start at `guides/00-unity-setup.md`.** Also see `guides/saving-and-committing.md` — a checklist to re-run every session, not a one-time guide. |
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
- [x] Guide 07 — Phase 1: monster stats as ScriptableObjects (Zombie converted)
- [x] Guide 08 — Phase 2: full monster roster — all five assets confirmed
      pushed and correctly configured (Zombie, Armored Zombie, Skeleton,
      Goblin, Cyclops).
- [x] Guide 09 — Phase 3: pike tower, catapult, praise-the-king tower —
      confirmed pushed: all four prefabs exist and `BuildModeController`'s
      Build Options list has all four entries, correctly wired. (Minor
      cosmetic typo: the Catapult's display name reads "Cataput Tower".)
- [x] Guide 9.5 (edits) — `guides/09.5-playtesting-edits.md`, playtesting-
      driven refinements layered on top of Guides 8/9 (consolidates what
      used to be separate Guides 10-13, plus the Tower DPS readout added
      after): King Damage as its own field, the Cyclops telegraph attack +
      pause/ramp, DPS readout (monsters and towers), inspector reorg,
      skeleton bone-pile fix, King-Priority/Keep-Target-Within-Range knobs,
      the structure-targeting rework (Structure Interest/Near-King Range +
      King-progress guard against perimeter-looping), and the Catapult
      impact mark. Everything in it is now confirmed pushed and correctly
      wired, including the Cyclops telegraph and Catapult impact mark
      pieces that were blocked pending Guide 09.
- [ ] Guide 10 — Phase 4: Walls, Gates & real pathfinding (code pushed).
      Monsters route around player-built mazes via `Core/PathGrid.cs` and
      break through only when no route exists. Editor work pending: add the
      PathGrid object, build the Wall and Gate prefabs, register them in
      Build Options.

**Next up:** the full build order is in [`ROADMAP.md`](ROADMAP.md). Work
through Guide 10, then ask Claude for Phase 5 (shop huts & player upgrades).
Note that Phase 4's Tile Weight Rule was deliberately skipped — the current
physical crowding between monsters is the preferred feel, and routing was
built so it stays untouched.

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
- **Sprite sort orders:** map 0–2, ground-effect markers 4–7 (Catapult
  impact mark 4; Cyclops telegraph boxes 5–7), coins 15, structures 19,
  characters 20–21, projectiles 25, health bars 30–31, placement ghost 40.
- **Placeholder policy (doc §1):** every visual is a tinted white Square/Circle
  sprite; sizes, timings and stats are Inspector fields — swapping in real art
  or tuning numbers must never require code changes.
- **Zero-value fields:** a numeric field that treats `0` as special always
  means one of two different things, and the field's Tooltip must say
  explicitly which one — never leave a bare "0 = ..." without saying what
  kind of default it is:
  - **Off** — the feature/behavior is disabled entirely (most range and
    window fields: King Priority Range, Keep Target Within Range, Structure
    Priority/Interest Range, Structure Near King Range, Recent Player
    Combat Window, Splash Radius, etc.).
  - **Auto** — not disabled, just automatically computed from another
    field instead of a fixed number (e.g. Impact Mark Diameter: `0` means
    match 2× Splash Radius, not "no mark").
  When adding a new field like this, say directly in the Tooltip whether
  `0` turns the thing off or just switches it to an automatic value tied to
  something else.
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
- **Save the Unity project (File → Save Project) before committing — not
  just Ctrl+S.** This has bitten us repeatedly (the full monster roster,
  Guide 09's tower prefabs, and twice with Guide 10's walls/gates). Full
  checklist to run every session: `guides/saving-and-committing.md`.
- **Two separate senses of "distance", kept apart on purpose.**
  `MonsterAI.DistanceBetween(a, b)` is straight-line edge-to-edge and backs
  every targeting *range* (attack range, the structure priority / interest /
  near-King ranges). `PathGrid` is route length and backs *movement*. Routing
  deliberately did not take over the ranges: they're "how far away is this"
  questions asked many times per frame and tuned against straight-line values.
  See `ROADMAP.md`'s Phase 4 section for the one rule that genuinely wants
  route length, and the cheap way to give it that.
- **Routing knows about static obstacles only — never other monsters.**
  `PathGrid`'s blocking layers are Structure and King, deliberately not Enemy.
  Monsters crowding, bumping and pushing past each other is plain physics plus
  `MonsterAI.SteerAroundNeighbors`, and it's the preferred feel — don't "fix"
  it by teaching routing about monsters, and note this is also why the doc's
  Tile Weight Rule (§7.1) remains unbuilt.
- **Player-built Walls/Gates carry a `Barrier` component**, and that is what
  separates "a wall to route around" from "a building worth attacking".
  Barriers are excluded from all discretionary structure targeting; they only
  ever become a target when routing reports no route exists at all (§6). Any
  future monster meant to smash walls on sight opts back in — it should not
  change that rule for everyone.
