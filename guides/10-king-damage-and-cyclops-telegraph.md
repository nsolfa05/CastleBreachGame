# Guide 10 — Unique King damage & the Cyclops telegraph attack

**Goal:** Two additions —
1. A per-monster **Unique King Damage** toggle: let a monster hurt the King by
   a different amount than it hurts the player/structures.
2. The **Cyclops** gets a proper **telegraphed area attack**: it winds up (a
   gray box fills in over the telegraph time), then slams a transparent-black
   box that damages everything caught inside it — giving you a window to dodge.

Both are driven by editable fields, all placeholder art.

**Update (Step 7 below):** the telegraph attack now also has an editable
pause/ramp — whether the Cyclops eases to a stop when it starts winding up,
and how fast it stops and gets moving again.

---

## Step 1 — Pull

GitHub Desktop → **Fetch** → **Pull**. Let Unity recompile; zero Console
errors expected.

## Step 2 — Unique King Damage (any monster)

Open any monster definition in `Assets/Monsters` (e.g. `Zombie`). Under
**Attacking the player / King** there are two new fields:

- **Use Unique King Damage** — checkbox, off by default.
- **King Damage** — the value used when the checkbox is on.

When **off** (default), nothing changes: the King takes **Player Damage**,
same as before. When **on**, the King instead takes **King Damage** — a
*replacement*, not a bonus (it doesn't stack). Structures still use Structure
Damage either way. So you can now have, say, a monster that barely scratches
the player but hits the King hard, or vice versa. Leave it off unless you want
that split for a particular monster.

## Step 3 — Add the telegraph visual component to the Monster prefab

The Cyclops's slam needs a component that draws the boxes. It goes on the ONE
shared `Monster` prefab (only monsters whose definition enables the attack ever
use it):

1. In `Assets/Prefabs`, double-click the **`Monster`** prefab (Prefab Mode).
2. **Add Component → Telegraphed Area Attack**.
3. Wire its one field: **Box Sprite** ← the **`Square`** sprite from
   `Assets/Sprites` (use the ⊙ picker).
4. (Optional) **Sorting Order** defaults to `5` — a ground marker drawn beneath
   structures and characters. Fine as-is.
5. Exit Prefab Mode.

## Step 4 — Turn on the telegraphed attack for the Cyclops

Open `Assets/Monsters/Cyclops`. Under the new **Telegraphed area attack**
header:

- **Uses Telegraphed Area Attack** — **check it**.
- **Attack Box Size** — `2, 2` (the doc's 2×2 zone). Editable.
- **Telegraph Time** — `1.5` (wind-up seconds). Editable.
- **Telegraph Base Color** — transparent gray (the attack area outline).
- **Telegraph Fill Color** — darker transparent gray (the charging fill).
- **Slam Color** — transparent black (the impact flash).
- **Slam Flash Seconds** — `0.2` (how long the black box shows).

Also make sure the Cyclops's **Attack Range** (under "Attacking the player /
King") is around `1.5` — this is how close it gets before it starts winding
up. And **Attack Interval** (`2.5`) is now the cooldown *between* slams, after
each one lands.

> How the attack plays out each cycle: the Cyclops stops, a gray box appears
> over its target's current spot and a darker box fills it in over Telegraph
> Time → at the end it slams (black flash) and damages everything inside the
> box → waits Attack Interval → repeats. Because the box is **locked** where
> the target was when the wind-up started, a quick player can step out of it
> before the slam lands.

## Step 5 — Playtest

- Send a Cyclops in (Wave 4 from Guide 08, or add one to any wave). Stand near
  it: it halts, the gray box appears and fills, then the black slam flashes.
  Step out during the wind-up and the slam misses you.
- Let the box catch you AND a tower at once — both take damage from one slam
  (everything inside the box is hit).
- Tune it live: raise **Telegraph Time** to make it easier to dodge, enlarge
  **Attack Box Size** to `3,3` to see the area grow, or recolor the boxes.
- Try **Unique King Damage** on a zombie: check it, set King Damage to `1`,
  and watch the King's health bar drop far slower than your own does when the
  same zombie hits you.

## Step 6 — Commit

`Unique King damage toggle and Cyclops telegraphed area attack`

## Step 7 — Telegraph pause/ramp (pull again for this part)

**Pull** again if you haven't already. Open `Assets/Monsters/Cyclops` — under
**Special: Cyclops — telegraphed area attack** there are three new fields
right after **Slam Flash Seconds**:

- **Pauses During Telegraph** — checkbox, **on** by default. When checked,
  the Cyclops eases to a stop as the wind-up begins and eases back up to
  speed once the slam lands and cooldown starts. When unchecked, it just
  keeps walking normally through the *entire* attack (wind-up and cooldown)
  — the box still locks in place and still lands on schedule, only the
  movement pause is skipped.
- **Telegraph Stop Duration** — seconds to decelerate to a full stop once
  winding begins. `0` = instant stop (the old hard-stop behavior). Default
  `0.2`. Only matters if Pauses During Telegraph is checked.
- **Telegraph Resume Duration** — seconds to accelerate back to full move
  speed once cooldown begins (right after the slam). `0` = instant resume.
  Default `0.3`. Only matters if Pauses During Telegraph is checked.

These are deliberately their own timers, separate from **Telegraph Time**
(how long the box takes to fill in) and **Attack Interval** (the cooldown
between slams) — so tuning how fast the Cyclops brakes/accelerates never
throws off your dodge-window or attack-cadence tuning.

### Playtest

- Raise **Telegraph Stop Duration** to `1.5` or so and watch the Cyclops
  visibly grind to a halt over that time instead of snapping to a stop the
  instant it starts winding up.
- Raise **Telegraph Resume Duration** the same way and watch it gradually
  pick back up to speed after the slam, instead of instantly resuming.
- Uncheck **Pauses During Telegraph** entirely — the Cyclops keeps walking
  right through the wind-up and cooldown; the gray/black boxes still appear
  and land on the locked spot exactly as before.
- Set both durations to `0` to confirm you get the original instant
  stop/start behavior back.

## Step 8 — Commit

`Cyclops telegraph pause toggle with tunable stop/resume ramp`

---

## ✅ Checkpoint

- [ ] Use Unique King Damage replaces (not stacks) King damage when checked
- [ ] Telegraphed Area Attack component on the Monster prefab, Square wired
- [ ] Cyclops winds up a filling gray box, then a black slam
- [ ] Dodging out of the box during wind-up avoids the hit
- [ ] The slam damages the player, King, and structures caught inside it
- [ ] Pauses During Telegraph checked: Cyclops eases to a stop, then eases
      back up to speed, using the two new duration fields
- [ ] Pauses During Telegraph unchecked: Cyclops keeps walking the whole time
- [ ] Committed & pushed
