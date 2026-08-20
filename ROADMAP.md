# Castle Breach — post-slice roadmap

The vertical slice (guides 00–05) is **done and playable**. This is the agreed
build order for everything else in the design doc, sequenced so that
foundational work comes first and each phase feeds the ones after it — not in
the doc's own listing order.

Working rhythm stays the same as the slice: Claude writes the code and a
click-by-click guide per phase; the designer does the Editor work and
playtests; commit + push at every checkpoint.

---

> **Status:** Phases 1–4 are **code-complete, pushed, and confirmed in the
> Editor** — guides 07 (foundations), 08 (monster roster), 09 (easy
> structures, plus Guide 9.5's playtesting-driven edits), and 10 (walls,
> gates, pathfinding) are all done. See README.md's Status section for the
> full breakdown.
>
> **Guide 11 — Combat & More is currently running, INSERTED ahead of Phase
> 5** at the user's explicit request (a full combat pass — knockback/stun,
> real weapons, combat UI, two new enemies, a new tower — felt more valuable
> to build now than shop huts). It is not part of this roadmap's original
> phase numbering; see its own writeup below, between Phase 4 and Phase 5.
> 11a–11d are done; 11e (Oil & Flame tower) is the last piece before Phase 5
> resumes.

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

## Phase 4 — Walls, Gates & real pathfinding (§6 + §7.1) ✅ code / guide 10

*The hardest core-gameplay chunk, isolated on purpose. Needs Phase 2's monster
variety and Phase 3's build-mode selection.*

- ✅ Player-built **Wall** (1×1, blocks everything) and **Gate** (blocks
  monsters except Goblin; player passes). Both are ordinary structures plus a
  `Barrier` component, which is what routing reads to tell a wall from a
  building.
- ✅ **Grid routing** (`Core/PathGrid.cs`) replacing straight-line movement —
  monsters route around player-built mazes, and break through when no route
  exists ("breakable if no path around it", §6). Notes on the design:
  - Obstacles come from a **physics scan** of the blocking layers, not from
    structures registering themselves, so any future structure type blocks
    correctly with no extra wiring and the grid can't silently disagree with
    actual physics.
  - **BFS, not A\***, because the same sweep that answers "is there a route"
    also produces the whole reachable region — which is exactly what the
    break-through fallback needs. At 40×30 the cost is irrelevant; swapping in
    A* for the reached case is a contained change inside `Solve()` if maps ever
    get big enough to care.
  - The break-through target is always chosen from the **boundary of the
    reachable region**, so breaking it always opens ground the monster couldn't
    otherwise get to. Currently the *nearest* such obstacle; the smarter
    version (simulate removing each candidate, keep whichever most shortens the
    route) drops in at the same spot, flagged in code.
  - Barriers are excluded from all *discretionary* structure targeting, so
    monsters never chew on a corridor they're merely walking through.
- ⬜ **A monster that attacks walls on sight** — a siege type that targets
  barriers proactively rather than only when trapped. Deliberately not built:
  it wants its own definition flag opting back into barrier targeting, not a
  change to the no-route-exists rule.
- ⬜ **Tile Weight Rule (§7.1)** — stacking cap of 6 per tile, push-aside
  behavior, player always counts as a full tile. **Deliberately deferred**: the
  current physical crowding is the preferred feel, and routing was built to
  leave it alone (monsters are not obstacles to each other). Revisit only if
  the doc's stacking behavior is actually wanted over that.
- ⬜ **Route length for the King-progress guard.** `MonsterAI.DistanceBetween`
  stays straight-line on purpose — see its own comment. Every targeting *range*
  is a "how far away" question, called many times per frame, tuned against
  straight-line values; routing them all would be an expensive silent retune.
  The one rule that genuinely wants route length is the King-progress guard
  inside Structure Interest Range, where a structure just the other side of a
  wall can read as closer-to-the-King than it is to walk to. Cheap to do
  properly when wanted: one breadth-first sweep outward *from the King* per
  movement class, refreshed only when `PathGrid.Version` changes, yields route
  distance for every tile at once.

## Guide 11 — Combat & More (inserted ahead of Phase 5)

*Not in the design doc's own phase list — the user chose to prioritize a full
combat pass (knockback/stun, real weapons beyond the placeholder sword swing,
combat feedback UI, two new enemies, a directional tower) before Phase 5's
shop huts. Built sub-guide by sub-guide, same rhythm as every other phase.
Full detail lives in `guides/11a`–`11d` and the two ideation docs the user
originally provided (not in this repo) — this is a compressed index, not a
replacement for reading the actual guides if picking this back up.*

1. **11a — Core framework + knockback/stun** ✅ code / guide 11a
   - `Combat/HitEffects` (embeddable knockback+stun struct, any attacker) +
     `Combat/KnockbackReceiver` (the receiving side; owns the Rigidbody2D
     while active, hands control back via `ControlSuppressed`).
   - Collapsible Inspector foldout sections (`Editor/FoldoutHeaderEditor`) —
     applies automatically to every `[Header]` on any inheriting script.
   - Per-enemy Stun Resistance.
2. **11b — Weapons** ✅ code / guide 11b
   - Sword reworked from a rectangle to a true angular arc (`PlayerAttack` +
     new `Player/PlayerAim` for shared aim direction).
   - Three new weapons — Bow, Hammer, Fire Staff — sharing a
     `Player/ChargedWeapon` hold-to-charge base class.
   - `Player/WeaponSwitcher` (the `V` weapon-select menu), mutually exclusive
     with `B`/build mode by design (see README.md's Conventions for the
     execution-order race this needed fixing).
   - Two new shared attack primitives: `Combat/StraightProjectile`
     (fixed-direction flight) and `Combat/BurnZone` (a generic ground DoT
     patch) — both explicitly built for reuse (Fire Staff, and later Faun in
     11d and the Oil & Flame tower in 11e).
3. **11c — Combat UI** ✅ code / guide 11c
   - `HealthBar` gets a Hide Until Damaged mode (monsters/towers now hide
     their bar at full health, opt-in per prefab).
   - Configurable gold loss on death (`PlayerRespawn`: Lose All / Percentage
     / Fixed Amount).
   - Top-left text indicator for which selection menu (`V`/`B`) is open.
   - `DeathEffect`/`DeathParticle` — shared death visual (red tint + a small
     hand-rolled particle burst) for the Player and any monster; the corpse
     visually lingers for a tunable duration before disappearing.
   - Deferred (explicitly, not forgotten): weapon aim-preview "ghost
     visuals" for Bow/Hammer/Fire Staff, matching the Sword's grey-idle/
     yellow-on-swing crescent (added as a follow-up fix, not originally
     part of 11b).
4. **11d — New enemies** ✅ code / guide 11d
   - **Redcap** — `MonsterDefinition.targetsOnlyPlayer`, the exact mirror of
     Goblin's existing `targetsOnlyKing`. Needed almost no new code — reuses
     the same routing/wall-breaking fallback every King-rushing monster
     already has.
   - **Faun** — a real new pattern: `MonsterAI.usesRangedAttack` →
     `UpdateRangedAttack`, firing a `StraightProjectile` that leaves a
     `BurnZone` on impact (no direct hit damage, matching the Fire Staff),
     retreating when hit at melee range (a distance-based proxy for "hit by
     melee" — real weapon-type tracking doesn't exist yet, flagged as a
     simplification), and stepping out of its own freshly-placed burn zone.
5. **11e — Oil & Flame tower** ⬜ not started
   - A directional 2×2 tower: rotate with arrow keys at placement, ghost
     preview shows the attack range dynamically as it's rotated. Attack
     pattern: 3 tiles forward, then expands to 3-wide × 2-deep; if a wall/
     structure blocks it before max range, the expansion point moves closer
     so the flame still forms correctly against the obstacle. Burn damage
     (reuse `Combat/BurnZone` — this is exactly the reuse case it was built
     for), with a toggle for whether it can also damage the player.
   - Also brings the **click-to-select-a-tower system** the user asked for
     (so a future upgrade/delete-tower feature has something to click on) —
     scoped for whenever placement/rotation is being built anyway.

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

- [x] **`13a`** — Title scene (Campaign/Survival-button-only/Test/Settings),
  Settings scene (Master Volume + Cursor Speed, both `PlayerPrefs`-backed),
  and a custom on-screen cursor replacing the OS pointer everywhere
  (`CustomCursor.cs`) — deliberately built so a future gamepad-driven
  cursor can slot in later (position integrated from stick input instead of
  eased toward a mouse target) without rework. See
  `guides/13a-title-and-settings.md`.
- [ ] **`13b`** — Campaign level-select screen: scrollable left→right world
  view, level nodes **zigzagging up/down at varying heights** (not a
  straight line), connected by a **splined (curved), not straight-angled,
  dashed trail**. Nodes are **named** (not just numbered slots) and
  **moveable** in the Editor — same drag-to-reposition pattern as the
  portfolio site's tree editor (click node, drag, save position). Starts as
  10 placeholder nodes at hand-set positions; migrates onto real campaign
  data once `14b` exists (small, expected rework moment, not a redo).
- [ ] **`13c`** — Win/lose screens gain "back to menu"; full scene-
  transition pass once Campaign exists too (Title ↔ Campaign ↔ Game ↔
  Settings, Back buttons everywhere they're needed).

## Phase 7 — Map Builder & level data (§3.5 + §10.5)

*Split into two passes at the user's request — level data as files first
(unblocks having more than one real level + Phase 6's level-select), then
the in-Editor designer tool itself soon after, not deferred long. Expect
ongoing follow-up passes whenever a new monster/structure/tile type is
added later — not a build-once phase, and that's fine; see the
extensibility note below.*

**Pass 1 — foundation:**

- [ ] **`14a`** — Variable grid size, built **immediately** (not deferred
  — explicitly wanted for map variety from the start, unlike the original
  plan to keep a fixed 40×30 grid). Real refactor: `Columns`/`Rows` are
  currently global `public const int` in `GridMath.cs`, shared by
  `CastleMapGenerator`, `TileRef`'s bounds-checking, `WaveSpawner`'s
  fallback spawn point, and `BuildModeController`'s placement bounds check
  — all of those need grid size to become per-level instance data instead
  of a compile-time constant.
- [ ] **`14b`** — Level data as files (ScriptableObject or JSON per
  §10.5): grid size (now variable, per `14a`), wall/gate/obstacle regions,
  King spawn, waves, King HP, starting gold, level **name**, and campaign
  **trail position** (for `13b`'s nodes). (This is where the README's
  deferred per-level items — King position, grid size, rock obstacles,
  per-level tile themes — all land.) Also where the README's fuller
  tile-*properties* vision naturally lands: water (blocking to ground
  units, shootable-over for ranged), breakable environment tiles (trees),
  and unbreakable rocks, as one coherent tile-type system rather than
  one-off features — ground hand-painting itself (visuals only, no
  gameplay behavior yet) is already live via Guide `12c`. Campaign levels
  load from these files; `13b`'s level-select nodes point at them for real.

**Pass 2 — the designer tool itself:**

- [ ] **`15a`** — Builder scaffold. **Editor-only** (same `Editor/`-folder
  pattern already used for `CreateGroundTileVariants`/
  `MoveTilesBetweenLayers`), so it's automatically stripped from real
  player builds and never reachable by players — this is the concrete
  mechanism behind "separate from the player's actual game." Create/load/
  save a level-data asset (`14b`), basic brush painting hooked to the now-
  variable grid (`14a`).
- [ ] **`15b`** — Builder power tools: **box/drag select**, **paint
  bucket** (flood fill), **line/drag tool** — on top of the basic brush
  from `15a`.
- [ ] **`15c`** — Live designer controls (start/stop test rounds, unlimited
  gold, spawn-rate/gate controls, §3.5 — Editor-time-only, same reasoning
  as `15a`) plus campaign customization UI: add/remove/reorder/move level
  nodes on the `13b` trail, easy to swap which level data asset a node
  points at.

**Extensibility (why "not a build-once tool" is fine):** the builder's
palette of placeable things should scan for definition assets (same
pattern `MonsterDefinition` already uses, and `BuildModeController`'s Build
Options list) rather than hardcode a list — adding a new monster/tower/
tile type later means creating a new definition asset, which then just
shows up in the builder. No builder code changes needed for ordinary new
content; only a genuinely new *category* of thing (not just a new instance
of an existing category) would need real builder work.

---

*Done so far: vertical slice (guides 00–05) — map, player, camera, King,
zombies, waves, currency, HUD, archer tower with targeting modes + range
circle, build mode with ghost preview.*
