# Guide 03 — The King & the zombies

**Goal:** The King (§7.4) sits at the map center with a visible health bar. Zombies
(§7.3) pour out of the gates in waves, march toward the King, chase you if you get
close, and die to your sword. If the King falls, the game freezes — game over
(the on-screen message comes in Guide 04).

You'll learn: prefabs (the most important Unity concept), health bars, and the
wave spawner.

---

## Step 1 — The King

1. Hierarchy → right-click → **2D Object → Sprites → Square**, name it **`King`**.
2. Select it and set:
   - **Position**: `20, 15, 0` (map center)
   - **Scale**: `1.8, 1.8, 1` (a stately presence, just under 2×2 tiles)
   - **Sprite Renderer → Color**: gold/purple — royal
   - **Order in Layer**: `20`
   - **Layer** (top of Inspector): `King`
3. **Add Component**:
   - **Box Collider 2D** (auto-sizes to the sprite — fine)
   - **Health** → set **Max Health** to `100` (§7.4 — and per the doc it stays
     Editor-tunable per level, which is exactly what this field is)

## Step 2 — A reusable health bar (your first prefab)

A **prefab** is a saved object template: build it once, stamp out copies, and edits
to the prefab flow into every copy. The health bar will hang over the King, every
zombie, and later every tower.

1. Right-click the `King` object → **Create Empty**, name it **`HealthBar`**.
   Set its **Position** to `0, 1.3, 0` (just above the King's head).
2. Right-click `HealthBar` → **2D Object → Sprites → Square**, name it
   **`Background`**: Color near-black, **Order in Layer `30`**. Leave its Scale
   at `1,1,1` — the script sizes it for you (see the note below).
3. Right-click `HealthBar` → **2D Object → Sprites → Square**, name it **`Fill`**:
   Color bright green, **Order in Layer `31`**. Leave its Scale at `1,1,1` too.
4. Select `HealthBar` → **Add Component → Health Bar** (our script). Wire:
   - **Health** ← drag the `King` object in (it finds its Health component)
   - **Background** ← drag the `Background` child in
   - **Fill** ← drag the `Fill` child in
   - **Bar Width** `1`, **Bar Height** `0.15` — this is the ONE place that sets
     the bar's size (see the note below).
5. Make it a prefab: create a folder `Assets/Prefabs`, then **drag the `HealthBar`
   object from the Hierarchy into that folder** in the Project panel. The
   Hierarchy entry turns blue — it's now an instance of the prefab.

> **How to size a health bar (read this — it's a common confusion):** the
> script scales **both** the Background and the Fill from its **Bar Width** /
> **Bar Height** fields, every frame. So set the size there and **nowhere
> else** — do **not** set the Background's or Fill's own Transform Scale by
> hand, because the script overwrites them. (Earlier the Background was scaled
> by hand while the Fill was scaled by the script, which is why bars looked
> mismatched from each other.) Want a wider King bar than a zombie bar? Change
> **Bar Width** on that instance's HealthBar component — that's it.

> Note: the **Health** reference is wired per instance (each bar watches a
> different owner), so you'll re-drag that one field whenever you add a bar to a
> new object. The look (bar size/colors) lives in the prefab; per-instance
> tweaks like a longer King bar are just overrides of Bar Width / Bar Height.

## Step 3 — The GameManager

1. Hierarchy → **Create Empty**, name it **`GameManager`**.
2. **Add Component → Game Manager** (our script). Wire all four references:
   - **Player** ← the `Player` object
   - **Player Health** ← the `Player` object again (it finds the Health component)
   - **King** ← the `King` object
   - **King Health** ← the `King` object again
3. **Starting Gold** is 200 — enough to test-buy one Archer Tower in Guide 05.

## Step 4 — The Zombie prefab

1. Hierarchy → **2D Object → Sprites → Square**, name it **`Zombie`**. Set:
   - **Scale**: `0.85, 0.85, 1`
   - **Color**: sickly dark green
   - **Order in Layer**: `20`
   - **Layer** (top of Inspector): `Enemy` (choose "Yes, change children" if asked)
2. **Add Component** each of:
   - **Rigidbody2D**: Gravity Scale `0`, Constraints → **Freeze Rotation Z**
   - **Box Collider 2D** (auto-size is fine)
   - **Health**: Max Health `10` (§7.3)
   - **Zombie AI**: the §7.3 numbers are pre-set (speed 4, damage 3 every 1.5s,
     notices you within 6 tiles). Set **Structure Layers** → check **Structure**.
     Leave **Currency Drop Prefab** empty for now — coins arrive in Guide 04.
3. Health bar: drag the **HealthBar prefab** from `Assets/Prefabs` **onto the
   `Zombie` object** in the Hierarchy (making it a child). Set its Position to
   `0, 0.7, 0`, and wire its **Health** field ← the `Zombie` object.
4. Now prefab the whole zombie: drag `Zombie` from the Hierarchy into
   `Assets/Prefabs`. Then **delete the Zombie from the Hierarchy** (right-click →
   Delete) — the spawner will create them at the gates.

## Step 5 — The wave spawner

1. Hierarchy → **Create Empty**, name it **`WaveSpawner`**.
2. **Add Component → Wave Spawner** (our script). Wire:
   - **Map** ← the `MapGenerator` object (that's how it knows where the gates are —
     spawns happen only on gate tiles, §3.3)
   - **Zombie Prefab** ← the `Zombie` prefab from `Assets/Prefabs`
3. Expand **Waves**: three waves are pre-configured (4 from the West gate, then 6
   from West+East, then 10 from all four gates). Everything — counts, pacing,
   gates — is editable here, no code.

## Step 6 — Play!

Press **▶** and watch:

- After 5 quiet seconds, zombies emerge from the **West gate** and march straight
  at the King.
- Stand in their path — within 6 tiles they turn and chase **you** instead. Lure
  them away, then kill them with **Space** (10 HP ÷ 2 damage = 5 swings each).
- Let one reach the King and watch the King's health bar shrink.
- Get mobbed and die — you vanish, respawn at your start point 3 seconds later.
- Let the King die: everything freezes. That's the lose state — the "GAME OVER /
  press R" text arrives with the HUD in Guide 04. For now, stop and restart Play.
- Clear all three waves and the game also freezes — that's victory (§10.2).

**Optional polish:** zombies shove each other around because they collide. If the
crowd looks too chaotic: **Edit → Project Settings → Physics 2D**, scroll to the
**Layer Collision Matrix**, and uncheck the **Enemy × Enemy** box. Zombies now
walk through each other but still collide with you, walls and structures. (The
doc's real answer is the §7.1 tile-weight system — a post-slice feature.)

## Step 7 — Save & commit

`King with health bar, zombie AI, gate wave spawner, win/lose states`

---

## ✅ Checkpoint

- [ ] King at map center with a working health bar
- [ ] Zombies spawn at gate tiles only, in three configurable waves
- [ ] Zombies target the King, switch to you inside 6 tiles, and drop to 5 sword hits
- [ ] Player respawns 3s after dying
- [ ] King's death freezes the game; clearing every wave also freezes it (victory)
- [ ] Zombie + HealthBar exist as prefabs in `Assets/Prefabs`

**Next:** [Guide 04 — Currency & HUD](04-currency-and-hud.md)
