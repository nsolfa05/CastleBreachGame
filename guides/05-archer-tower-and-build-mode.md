# Guide 05 — Archer Tower & build mode

**Goal:** The final vertical-slice piece. Press **B** for a grid-snapped ghost
preview (§10.3), click to spend 150 gold on an **Archer Tower** (§6) that
auto-fires arrows at the nearest zombie — and zombies fight back, attacking
towers that block their way. After this guide the full game loop works:
*defend → earn → build → defend better*.

---

## Step 1 — The arrow (projectile) prefab

1. Hierarchy → **2D Object → Sprites → Circle**, name it **`Arrow`**. Set:
   - **Scale**: `0.15, 0.15, 1`, **Color**: dark gray/black, **Order in Layer**: `25`
2. **Add Component → Projectile** (our script). No collider needed — it homes in
   on its target and hits at close range.
3. Drag into `Assets/Prefabs`, delete from the Hierarchy.

## Step 2 — The Archer Tower prefab

1. Hierarchy → **2D Object → Sprites → Square**, name it **`ArcherTower`**. Set:
   - **Scale**: `1.8, 1.8, 1` (its footprint is 2×2 tiles; slightly smaller looks tidier)
   - **Color**: tan/brown, **Order in Layer**: `19`
   - **Layer** (top of Inspector): `Structure`
2. **Add Component** each of:
   - **Box Collider 2D** (auto-size ≈1.8 — good: solid to zombies and the player)
   - **Health** → Max Health `50` (§6)
   - **Destroy When Dead** (our script — the tower is removable by force)
   - **Archer Tower** (our script): damage 4, one shot per second, range 6 tiles
     pre-set from §6. Wire:
     - **Enemy Layers** → check **Enemy**
     - **Projectile Prefab** ← the `Arrow` prefab
3. Health bar: drag the **HealthBar prefab** onto `ArcherTower` (as a child),
   Position `0, 1.2, 0`, and wire its **Health** field ← the `ArcherTower` object.
4. Drag `ArcherTower` into `Assets/Prefabs`, then delete it from the Hierarchy.

*(With the tower selected in the Project panel you can see its cyan range circle
in the Scene view whenever you place one — the script draws it as a gizmo.)*

## Step 3 — The placement ghost

1. Hierarchy → **2D Object → Sprites → Square**, name it **`PlacementGhost`**.
   Set **Order in Layer** to `40` (renders above everything). Position/scale/color
   don't matter — the build script controls them.
2. In the Inspector, **uncheck the box next to its name** (top-left) to deactivate
   it — it should only appear during placement.

## Step 4 — The build mode controller

1. Select the **`GameManager`** object → **Add Component → Build Mode Controller**
   (our script). Wire:
   - **Tower Prefab** ← the `ArcherTower` prefab (cost 150 pre-set, §6)
   - **Wall Tilemap** ← the `Walls` object; **Gate Tilemap** ← `Gates`
   - **Blocking Layers** → check **Player, Enemy, Structure, King**
   - **Ghost** ← the `PlacementGhost` object

## Step 5 — Play the full loop!

- Press **B**: a translucent 2×2 square sticks to the grid under your mouse.
  **Green** = buildable & affordable; **red** = not (over a wall/gate, on top of
  someone, or you're broke). **Left-click** places it (gold drops by 150);
  **right-click/Esc** cancels.
- Place a tower in front of the West gate and watch it pelt arrows — 4 damage,
  once per second: it kills a zombie alone in ~3 seconds, faster with your sword
  helping.
- Watch zombies attack a tower that stands between them and the King — and
  destroy it if you don't defend it (50 HP, 3 damage per zombie every 1.5s).
- The real strategy loop: you start with 200 gold (one tower + change); each
  zombie pays 3. Can you afford a second tower by Wave 2, and hold all four gates
  by Wave 3?

## Step 6 — Save, commit, and celebrate

`Archer tower with build-mode placement — vertical slice complete`

---

## ✅ Vertical slice — final checklist

Everything from the design doc's §11 priority list now works:

- [ ] 40×30 grid with the doc's exact wall/gate coordinates (§3.1–3.3)
- [ ] WASD knight with mouse-aimed sword, §7.2 stats, respawn (§10.1)
- [ ] Camera follow + zoom with speed-by-zoom curve (§3.7)
- [ ] King with tunable 100 HP, lose on death (§7.4, §10.2)
- [ ] Zombies with §7.3 stats, gate-only spawning, wave system (§8)
- [ ] Currency drops with §4 rules; kills fund construction
- [ ] Archer Tower with §6 stats and the §10.3 placement flow
- [ ] Win/lose + restart

## What's next (pick what sounds fun — ask Claude to build any of these)

Roughly in the order the doc suggests:

1. **More monsters** (§7.3) — Skeleton (bone-pile revive!), Armored Zombie,
   Goblin (king-seeker), Cyclops. This is also when monster stats should move
   into ScriptableObjects (designer-editable asset files) instead of per-prefab
   fields.
2. **More structures** (§6) — Pike Tower, Catapult (AoE + dead zone), Praise the
   King Tower (economy), Walls & Gates — which forces real **pathfinding** and
   the §7.1 tile-weight rule.
3. **Shop huts** (§3.6, §5) — walkable hut tiles, the darkening menu overlay,
   player upgrades, and moving tower-buying into the Defense Hut.
4. **Title screen & scenes** (§2) — menu, campaign level select, settings.
5. **Map Builder** (§3.5, §10.5) — the designer tool, with levels saved as data
   files. The groundwork exists: maps are already just lists of tile regions.

When you're ready, start a Claude session on this repo and say what you want —
the code lives in `CastleBreach/Assets/Scripts`, and since your scenes and
prefabs are committed too, Claude can read and reason about all of it.
