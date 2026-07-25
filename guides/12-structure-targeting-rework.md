# Guide 12 — King Damage simplified, structure targeting reworked

**Goal:** A cleanup pass on monster targeting/damage, based on what came up
tuning the Zombie: two redundant checkboxes removed, and the "detour to a
structure vs. head to the King" logic reworked so a monster can no longer
get stuck looping around the outside of the map, forever finding "the
nearest structure" and never actually closing distance on the King.

---

## Step 1 — Pull

GitHub Desktop → **Fetch** → **Pull**. Let Unity recompile; zero Console
errors expected. Open `Assets/Monsters/Zombie` afterward — you'll see the
field changes described below.

## Step 2 — King Damage is now always its own value

**Use Unique King Damage** is gone. **King Damage** is now a plain field
(same as Player Damage / Structure Damage) — always used for hits on the
King, never a fallback to Player Damage, never stacked with anything. If
you want a monster to hit the King the same as it hits the player, just set
King Damage equal to Player Damage by hand. On Zombie specifically nothing
changes — both are `3` already.

## Step 3 — Prioritizes Structures checkbox is gone

It was redundant: **Structure Priority Range** and **Structure Interest
Range** (next step) already mean "off" at `0`, the same convention every
other range field here uses (King Priority Range, Keep Target Within Range,
etc.). If you previously had Prioritizes Structures checked, just make sure
Structure Priority Range and/or Structure Interest Range are set to the
values you want — the checkbox added no behavior beyond what those two
already gate.

## Step 4 — Structure Far King Ratio replaced by Structure Interest Range

The old ratio math (**Structure Far King Ratio** / **Structure Notice
Radius**) is gone, replaced by one field: **Structure Interest Range**
(tiles, `0` = off).

- Within this range, a monster prefers a structure over trekking to the
  King — same as before, still **only** competes with heading to the King
  (if the monster's already chasing the player, this tier is skipped
  outright, no exceptions).
- **New guard:** the structure must also be closer to the King than the
  monster currently is. This is what stops a monster from getting lured
  sideways or backward — every structure-detour is now guaranteed to be a
  step *toward* the King, not away from it. This is the fix for "monster
  circles the outside of the map hopping between distant towers and never
  reaches the King."

**Zombie's old tuning (`Structure Far King Ratio: 3`, `Structure Notice
Radius: 10`) is gone** — pull will leave **Structure Interest Range at
`0` (off)** until you set it. Try **`8`** as a starting point and retune
by feel.

> **Forward-looking note (for when walls/pathfinding land):** this King-
> distance comparison is computed via one shared internal helper
> (`MonsterAI.DistanceBetween`) that's currently straight-line, same as all
> movement right now. When real pathfinding arrives, that's the one place
> to swap in actual path length — once that happens, breaking a wall that
> opens a new shortest route will automatically make structures along it
> start qualifying as "on the way," with no further changes needed here.
> Noted in `ROADMAP.md` under Phase 4 so it isn't missed.

## Step 5 — New: Structure Near King Range

**Structure Near King Range** (tiles, `0` = off) — if a structure this
monster would otherwise target (from *either* range above) is within this
many tiles of the King, it's skipped entirely and the monster goes straight
for the King instead. For structures built defensively right next to the
King, where attacking them isn't worth ignoring the real target. Applies to
both Structure Priority Range and Structure Interest Range candidates.

## Step 6 — Playtest

- Set **Structure Interest Range** on Zombie to `8`, place a tower roughly
  on the way to the King, and confirm zombies detour to it instead of
  beelining past.
- Place a second tower well off to the side (not on the way to the King) —
  confirm zombies do **NOT** detour to it, even if it's within Structure
  Interest Range, since it isn't closer to the King than they already are.
- Set **Structure Near King Range** to `2` and place a tower right next to
  the King — confirm zombies ignore it and go straight for the King.
- Set **King Damage** different from **Player Damage** on a test monster
  and confirm the King's health bar drops at the new rate.

## Step 7 — Commit

`Rework structure targeting: King-progress guard, Structure Interest/Near-King range, remove redundant toggles`

---

## ✅ Checkpoint

- [ ] King Damage is a plain always-used field, no checkbox
- [ ] Prioritizes Structures checkbox is gone; range fields alone control it
- [ ] Structure Interest Range replaces the old ratio fields
- [ ] A monster only detours to a structure that's actually closer to the King than it is
- [ ] Structure Near King Range skips structures too close to the King
- [ ] Committed & pushed
