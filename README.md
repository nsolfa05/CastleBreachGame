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

## Conventions (for future work — human or Claude)

- **Grid:** 40×30 tiles, 1 tile = 1 world unit, map's bottom-left at world
  (0,0). Doc coordinates ("A1".."AN30", row 1 at the top) are parsed by
  `TileRef`; all conversions go through `GridMath`. Never hand-convert.
- **Layers:** 6 Player, 7 Enemy, 8 Structure, 9 King.
- **Sprite sort orders:** map 0–2, coins 15, structures 19, characters 20–21,
  projectiles 25, health bars 30–31, placement ghost 40.
- **Placeholder policy (doc §1):** every visual is a tinted white Square/Circle
  sprite; colors, sizes, timings and stats are Inspector fields — swapping in
  real art or tuning numbers must never require code changes.
- **Stats carry doc-section comments** (`§6`, `§7.3`, …) so numbers can be
  traced back to the design doc.
- The design doc's roadmap after the slice is summarized at the end of
  `guides/05-archer-tower-and-build-mode.md`.
