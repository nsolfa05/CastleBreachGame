# Guide 09 — Phase 3: Easy structures (§6)

**Goal:** Three new buildables join the Archer Tower: the cheap melee **Pike
Tower**, the long-range **Catapult** (splash damage + a dead zone it can't
hit inside), and the **Praise the King Tower** (prints gold — and lures
Goblins). Build mode grows number-key selection: **1–4 pick a structure**,
left-click places it.

> **After pulling:** the tower script is now called **Attack Tower** (it
> drives all three combat towers; only the numbers differ per prefab). Your
> ArcherTower prefab keeps all its settings automatically — you'll just see
> the new name, plus two new fields (Min Range, Splash Radius). Also: the
> range circle now sizes itself correctly (it used to inherit the tower's
> 2× body scale — that's why it looked too big), and it shows the Catapult's
> dead zone as an inner red circle.

---

## Step 1 — Pull & verify the Archer Tower survived the rename

1. GitHub Desktop → **Fetch** → **Pull**; let Unity compile, zero errors.
2. Open the `ArcherTower` prefab: the component reads **Attack Tower
   (Script)** and all your old values are intact (damage 4, range 6,
   targeting mode, projectile). Two new fields sit at defaults: **Min
   Range 0**, **Splash Radius 0**. Exit.

## Step 2 — Pike Tower (cheap melee, 1×1)

1. In `Assets/Prefabs`, select `ArcherTower`, **Ctrl+D** to duplicate,
   rename the copy **`PikeTower`**.
2. Double-click it (Prefab Mode) and change:
   - **Transform → Scale**: `0.9, 0.9, 1` (it's a 1×1 building)
   - **Sprite Renderer → Color**: darker brown (distinct from Archer's tan)
   - **Attack Tower**: Damage **2**, Seconds Between Shots **1**, Range
     **1.5**, and **clear the Projectile Prefab field** (select it, press
     Delete/Backspace so it reads `None`) — an empty projectile field means
     *instant melee stab*.
   - **Health → Max Health**: **20**
3. Exit Prefab Mode.

## Step 3 — Catapult (splash + dead zone, 2×2)

1. First its stone: duplicate the `Arrow` prefab, rename **`CatapultStone`**.
   Open it: **Scale** `0.3, 0.3, 1`, color dark gray, and on **Projectile**
   set **Speed = 4** (slow = the doc's "hangs in the air" feel). Exit.
2. Duplicate `ArcherTower` again, rename **`CatapultTower`**. Open it:
   - **Sprite Renderer → Color**: slate gray
   - **Attack Tower**: Damage **3**, Seconds Between Shots **5**, Range
     **7**, **Min Range 3** (the dead zone!), **Splash Radius 1**,
     **Projectile Prefab** ← `CatapultStone`
   - **Health → Max Health**: **30**
3. Exit. *(Its range circle now shows an inner red circle — the zone it
   cannot fire into. Anything that gets that close is safe from it.)*

## Step 4 — Praise the King Tower (economy, 2×2)

This one's built from scratch — it doesn't attack, so no Attack Tower:

1. Hierarchy → **2D Object → Sprites → Square**, name it
   **`PraiseTheKingTower`**. Set:
   - **Scale** `1.8, 1.8, 1`, **Color** royal gold, **Order in Layer** `19`
   - **Layer** (top of Inspector): **Structure**
2. **Add Component** each of: **Box Collider 2D**, **Health** (Max **30**),
   **Destroy When Dead**, **Praise The King Tower** (our script — 3 gold per
   4s pre-set, tunable).
3. Health bar: drag the **HealthBar prefab** onto it as a child, Position
   `0, 1.2, 0`, wire the bar's **Health** ← this tower, **Background** ←
   its Background child, **Fill** ← its Fill child.
4. Drag it into `Assets/Prefabs`, **delete it from the Hierarchy**.

## Step 5 — Register all four in build mode

1. Select **`GameManager`** → **Build Mode Controller**. The old single
   "Tower Prefab" field is gone; there's now a **Build Options** list.
2. Set its **size to 4** and fill in:

| # (hotkey) | Display Name | Prefab | Cost | Footprint |
|---|---|---|---|---|
| 1 | Archer Tower | ArcherTower | 150 | 2, 2 |
| 2 | Pike Tower | PikeTower | 80 | **1, 1** |
| 3 | Catapult | CatapultTower | 250 | 2, 2 |
| 4 | Praise the King | PraiseTheKingTower | 120 | 2, 2 |

3. Confirm **Wall Tilemap / Gate Tilemap / Blocking Layers** are still set
   from before. **Ctrl+S.**

## Step 6 — Playtest

- Press **1/2/3/4** — each picks up that structure's ghost (numbers switch
  mid-carry too). **B** re-carries the last one. Left-click places, and you
  keep carrying to place more; right-click/Esc stops.
- **Pike** (80g): tiny footprint snaps to single tiles; it silently stabs
  anything adjacent. Cheap gate-plug for early waves.
- **Catapult** (250g): watch the slow stone sail in and hit a *cluster* —
  multiple zombies lose HP at once. Then let a zombie get close to the
  catapult: inside the red inner circle it's untouchable — a Catapult needs
  bodyguards (a Pike next to it is a classic pairing).
- **Praise the King** (120g): gold ticks up 3 every 4 seconds. It pays for
  itself in ~2.5 minutes — build early or don't bother. Now the fun part:
  place one within ~4 tiles of a Goblin's route and watch Goblins swerve off
  the King to attack it (hitting it for 4 per swing!). It's Goblin bait —
  the doc's intended strategic tradeoff.
- Click any placed tower — range circle (and the Catapult's dead zone)
  toggles, correctly sized now.

## Step 7 — Commit

`Phase 3: pike tower, catapult with splash + dead zone, praise the king tower`

---

## ✅ Checkpoint

- [ ] Archer prefab intact under the renamed Attack Tower component
- [ ] Pike: melee (no projectile), 1×1 snapping works
- [ ] Catapult: splash hits groups; can't hit inside its red dead zone
- [ ] Praise the King: generates gold; Goblins divert to it within 4 tiles
- [ ] Number keys 1–4 select; B recalls last; costs deduct correctly
- [ ] Committed & pushed

**Next phase (ask Claude when ready):** Phase 4 — Walls, Gates, real
pathfinding, and the §7.1 tile-weight rule. The hardest chunk; brand-new
guide + code when you get there.
