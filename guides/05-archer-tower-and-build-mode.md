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
     - **Targeting** → choose **Nearest Each Shot** (always retarget the closest
       enemy) or **Finish Current Target** (lock onto one enemy until it dies or
       leaves range, then retarget). Your call — tune it live and see which you
       prefer.
3. Health bar: drag the **HealthBar prefab** onto `ArcherTower` (as a child),
   Position `0, 1.2, 0`, and wire its **Health** field ← the `ArcherTower` object.
4. Drag `ArcherTower` into `Assets/Prefabs`, then delete it from the Hierarchy.

> **⚠️ Three things MUST be right on the tower or zombies won't be able to
> damage it** (the zombie looks for structures on the Structure layer that have
> health, and can only hit ones with a collider):
> 1. **Layer = `Structure`** (top of the Inspector) — not Default.
> 2. It has a **Box Collider 2D**.
> 3. It has a **Health** component.
> If a tower ever seems invincible to zombies, check these three first.

> **⚠️ Also check the checkbox right next to the `ArcherTower` name field, top-left
> of the Inspector — make sure it's CHECKED (active).** If a prefab's root object
> ever gets saved inactive, every copy of it (the build-mode ghost, and every
> placed tower) spawns invisible and completely inert — no errors anywhere,
> gold still gets spent when "placed," and in the Hierarchy its name shows in
> grayed-out text instead of normal white. That grayed-out name is the tell:
> it means "this object is inactive," and it's easy to miss. `BuildModeController`
> now force-activates both the ghost and placed towers as a safety net, but
> it's worth fixing at the source too.

*(With the tower selected in the Project panel you can see its cyan range circle
in the Scene view whenever you place one — the script draws it as a gizmo.)*

## Step 3 — The build mode controller

No separate ghost object to make — the controller builds its own preview by
cloning the tower prefab at runtime (so the ghost is always the right size, and
placed towers always keep their own color).

1. Select the **`GameManager`** object → **Add Component → Build Mode Controller**
   (our script). Wire:
   - **Tower Prefab** ← the `ArcherTower` prefab (cost 150 pre-set, §6)
   - **Wall Tilemap** ← the `Walls` object; **Gate Tilemap** ← `Gates`
   - **Blocking Layers** → check **Player, Enemy, Structure, King**

## Step 4 — Play the full loop!

- Press **B**: a translucent 2×2 tower ghost follows your mouse, snapped so the
  cursor sits between the four tiles it will cover. **Green** = buildable &
  affordable; **red** = not (over a wall/gate, on top of someone, or you're
  broke). **Left-click** places a real tower (gold drops by 150) and you stay in
  build mode, so you can place another right away; **right-click/Esc/B** exits.
- Place a tower in front of the West gate and watch it pelt arrows — 4 damage,
  once per second: it kills a zombie alone in ~3 seconds, faster with your sword
  helping.
- Watch zombies attack a tower that stands between them and the King — and
  destroy it if you don't defend it (50 HP, 3 damage per zombie every 1.5s).
  (If they walk straight past without hitting it, the tower isn't in their
  direct path to the King — zombies only attack structures blocking their way,
  not every tower on the map.)
- The real strategy loop: you start with 200 gold (one tower + change); each
  zombie pays 3. Can you afford a second tower by Wave 2, and hold all four gates
  by Wave 3?

## Step 5 — Save, commit, and celebrate

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

## What's next

The full prioritized build order for everything after the slice lives in
[`ROADMAP.md`](../ROADMAP.md) at the repo root — seven phases, sequenced so
foundational work (monster stats as ScriptableObjects) comes first and each
phase feeds the later ones.

When you're ready, start a Claude session on this repo and say which phase to
build — the code lives in `CastleBreach/Assets/Scripts`, and since your scenes
and prefabs are committed too, Claude can read and reason about all of it.
