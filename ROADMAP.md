# Castle Breach — post-slice roadmap

The vertical slice (guides 00–05) is **done and playable**. This is the agreed
build order for everything else in the design doc, sequenced so that
foundational work comes first and each phase feeds the ones after it — not in
the doc's own listing order.

Working rhythm stays the same as the slice: Claude writes the code and a
click-by-click guide per phase; the designer does the Editor work and
playtests; commit + push at every checkpoint.

---

> **Status:** Phases 1–3 are **code-complete and pushed** — the Editor-side
> work is written up as guides 07 (foundations), 08 (monster roster), and
> 09 (easy structures). Phase 4 code starts after those are verified
> in-Editor.

## Phase 1 — Foundations: monster stats as ScriptableObjects ✅ code / guide 07

*Why first: every later phase (new monsters, wave design, shop pricing, map
builder) reads this data. Doing it before adding four more monsters means the
migration happens once, on one monster, instead of five times later.*

- `MonsterDefinition` ScriptableObject: speed, health, damage numbers, attack
  intervals, ranges, tile weight, currency drop, sprite/color placeholder —
  one `.asset` file per monster type, editable without code (doc §1 policy).
- Refactor `ZombieAI` + Zombie prefab to read from `Zombie.asset`.
- `WaveSpawner` upgrade: waves become lists of (monster type, count, gate) so
  mixed waves work as soon as a second monster exists.

## Phase 2 — The monster roster (§7.3) ✅ code / guide 08

*Ordered easiest → hardest inside the phase; each one exercises the Phase 1
foundation.*

1. **Armored Zombie** — pure stat variant (HP 20, structure damage 5). Almost
   free once Phase 1 exists; proves the ScriptableObject pipeline.
2. **Goblin** — ignores the player entirely, passes through Gates, targets
   only the King (the "unless a Praise the King Tower is within 4 tiles" rule
   activates in Phase 3 when that tower exists).
3. **Skeleton** — two lives: dies → invulnerable bone pile for 6s → revives.
   First monster with a real state machine.
4. **Cyclops** — 2×2 hitbox, tile weight 6, prioritizes structures within 6
   tiles, slow lock-on attack with zone persistence (§7.3 detail note).

## Phase 3 — Easy structures (§6, the non-blocking ones) ✅ code / guide 09

*Why before walls/gates: these reuse the existing tower pipeline almost
unchanged — no pathfinding needed. Praise the King Tower also completes the
Goblin's targeting rule from Phase 2.*

1. **Pike Tower** — cheap 1×1 melee tower (the odd-footprint snapping in
   BuildModeController already handles 1×1).
2. **Praise the King Tower** — generates 3 gold / 4s instead of attacking;
   first economy building; Goblins divert to it within 4 tiles.
3. **Catapult Tower** — AoE splash (2×2), slow projectile with hang time, and
   the 6×6 dead zone (can't hit adjacent enemies).
4. Build mode grows a structure *selection* (number keys for now — the real
   Defense Hut menu arrives in Phase 5), and structure stats likely move to a
   `StructureDefinition` ScriptableObject mirroring Phase 1.

## Phase 4 — Walls, Gates & real pathfinding (§6 + §7.1)

*The hardest core-gameplay chunk, isolated on purpose. Needs Phase 2's monster
variety (weights differ per monster) and Phase 3's build-mode selection.*

- Player-built **Wall** (1×1, blocks everything) and **Gate** (blocks monsters
  except Goblin; player passes).
- **Grid pathfinding** (A*) replacing straight-line movement — monsters route
  around player-built mazes, and break walls when no path exists ("breakable
  if no path around it", §6).
- **Tile Weight Rule (§7.1)** — stacking cap of 6 per tile, push-aside
  behavior, player always counts as a full tile. (Tracked in README deferred
  notes; single-monster stacking couldn't test this before Phase 2.)

## Phase 5 — Shop huts & player upgrades (§3.6 + §5)

*Turns the systems into the real game loop. After this the doc's single-level
experience is feature-complete.*

- Hut tiles on the map (the four §3.6 locations, two still unassigned by
  design — stubs are fine per §9).
- Step-on-hut → menu opens with the darkening overlay, game keeps running;
  step-off → closes. Green/red/yellow affordability coloring, hover tooltips.
- **Player Upgrade Hut** — the §5 upgrade list (armor, damage, regen, speed),
  including the §10.1 "keep upgrades on death" Editor toggle.
- **Defense Building Hut** — tower/wall buying moves here; the B-key shortcut
  retires (it was always the stand-in for this menu).

## Phase 6 — Title screen & scenes (§2)

*Light, mostly-independent work — a good breather after Phases 4–5, and a
prerequisite for campaign structure. Teaches Unity scene management.*

- Title scene: Campaign / Survival (button only, §2) / Test / Settings.
- Campaign level-select screen (10 placeholder slots), Back buttons.
- Settings: sound + cursor speed. Win/lose screens gain "back to menu".

## Phase 7 — Map Builder & level data (§3.5 + §10.5)

*Last because it consumes everything: it authors levels that reference every
monster, structure, and rule built above.*

- Level data as files (ScriptableObject or JSON per §10.5): grid size, wall/
  gate/obstacle regions, King spawn, waves, King HP, starting gold.
  (This is where the README's deferred per-level items — King position, grid
  size, rock obstacles, per-level tile themes — all land.)
- The designer-only builder tool: start/stop test rounds, unlimited gold,
  spawn-rate/gate controls (§3.5) — and the long-deferred **hand-editing of
  map tiles**.
- Campaign levels load from these files; level-select slots point at them.

---

*Done so far: vertical slice (guides 00–05) — map, player, camera, King,
zombies, waves, currency, HUD, archer tower with targeting modes + range
circle, build mode with ghost preview.*
