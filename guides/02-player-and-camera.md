# Guide 02 — Player & camera

**Goal:** The Hero Knight (§7.2) walks with WASD, aims at the mouse, swings his
sword with spacebar (with a visible swing flash), collides with walls, and the
camera follows him with scroll-wheel zoom whose follow speed changes with zoom
(§3.7).

You'll learn: layers, tags, Rigidbody2D physics, parent/child objects, and wiring
Inspector references.

---

## Step 1 — Create the physics layers

Layers let scripts ask targeted questions like "what enemies are in this circle?".
Set up all four now so later guides can just use them:

1. **Edit → Project Settings → Tags and Layers** (expand the **Layers** section).
2. In the empty **User Layer** slots, type (order doesn't matter but the guides use these):
   - User Layer 6: `Player`
   - User Layer 7: `Enemy`
   - User Layer 8: `Structure`
   - User Layer 9: `King`
3. Close Project Settings.

## Step 2 — Build the player object

1. Hierarchy → right-click → **Create Empty**, name it **`Player`**.
2. With `Player` selected, in the Inspector:
   - Set **Position** to `X 20, Y 12, Z 0` (just south of map center, where the
     King will sit).
   - Top of the Inspector: set the **Tag** dropdown to `Player` (it's a built-in
     tag — coin pickups use it).
   - Next to it, set the **Layer** dropdown to `Player`. If a dialog asks about
     children, choose **Yes, change children**.
3. Give it a body: right-click `Player` in the Hierarchy → **2D Object → Sprites →
   Square**, name the child **`Body`**. Select `Body`:
   - **Sprite Renderer → Color**: pick a blue (the knight).
   - **Scale**: `0.9, 0.9, 1` (slightly smaller than a tile).
   - **Sprite Renderer → Additional Settings → Order in Layer**: `20` (entities
     render above the map).
4. Aim pivot + swing visual (the mouse-aimed sword arc):
   - Right-click `Player` → **Create Empty**, name it **`AimPivot`**. Make sure its
     Position is `0, 0, 0`.
   - Right-click `AimPivot` → **2D Object → Sprites → Square**, name it
     **`SwingArc`**. Select it and set:
     - **Position** `1, 0, 0` (one tile ahead of the player — the pivot rotates it
       around the player toward the mouse)
     - **Scale** `1, 3, 1` (1 tile deep, 3 tiles wide — the doc's swing arc)
     - **Color**: white with **A(lpha) around 130** (semi-transparent)
     - **Order in Layer**: `21`
   - Don't disable anything — the PlayerAttack script hides the arc automatically
     and flashes it only during a swing.

## Step 3 — Add physics + the scripts

Select the `Player` root object and **Add Component** each of these:

1. **Rigidbody2D** — makes it a physics object. Set:
   - **Gravity Scale**: `0` (top-down game — nothing falls)
   - **Collision Detection**: `Continuous`
   - **Interpolate**: `Interpolate` (smoother camera follow)
   - Expand **Constraints** → check **Freeze Rotation Z** (the knight never spins)
2. **Box Collider 2D** — his solid body. Set **Size** to `0.9, 0.9`.
3. **Player Movement** (our script) — Move Speed is pre-set to 5 (§7.2).
4. **Player Attack** (our script). Wire it up:
   - **Hit Layers**: dropdown → check **Enemy** (only)
   - **Aim Pivot** ← drag the `AimPivot` child from the Hierarchy
   - **Swing Visual** ← drag the `SwingArc` child
   - Damage 2, cooldown 0.5, reach 1, arc width 3 are pre-set from §7.2.
5. **Health** (our script) — set **Max Health** to `20` (§7.2).
6. **Player Respawn** (our script) — respawn delay 3s, tunable (§10.1).

## Step 4 — Camera follow

1. Select **Main Camera** → **Add Component** → **Camera Follow** (our script).
2. Set **Target** ← drag the `Player` object in.
3. Look at **Follow Speed By Zoom** — it's a curve (click it to open the curve
   editor): x=0 is fully zoomed in, x=1 fully zoomed out; the y value is how
   tightly the camera tracks. This is the doc's §3.7 "follow speed scales with
   zoom" rule as an editable curve. Leave the default for now.

## Step 5 — Play!

Press **▶ Play** and try:

- **WASD** moves the knight; diagonals aren't faster.
- Walk into a wall — you stop (that's the Tilemap Collider from Guide 01).
- Walk through a **gate** — you pass through (no collider there). *(Real gates
  block monsters only via game rules later; the doc's player can pass §6.)*
- Move the mouse around the knight, press **Space** — the white arc flashes on the
  mouse's side, once per half-second at most.
- **Scroll** to zoom out until the whole map is visible, back in close. Notice the
  camera follows snappily when close and lazily when far.

Two editor tricks while in Play mode:

- Select `Player` and watch the Inspector live — velocity, health, etc.
- Tweak any number (e.g. Move Speed to 12) to feel it — **but remember Play-mode
  changes revert when you stop**. To keep a value, note it and set it again after
  stopping.

## Step 6 — Save & commit

Stop Play mode, **Ctrl+S**, commit & push:
`Player movement, mouse-aimed sword swing, follow camera with zoom`

---

## ✅ Checkpoint

- [ ] WASD movement, blocked by walls, passes through gates
- [ ] Space flashes a 3-wide arc toward the mouse, max twice per second
- [ ] Scroll zooms between close-up and whole-map; follow tightness changes with it
- [ ] Layers Player/Enemy/Structure/King exist in Project Settings
- [ ] Scene saved, work committed

**Next:** [Guide 03 — The King & the zombies](03-king-and-zombies.md)
