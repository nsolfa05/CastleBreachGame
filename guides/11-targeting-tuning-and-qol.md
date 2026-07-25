# Guide 11 — Targeting tuning, DPS readout & skeleton fix

**Goal:** A batch of quality-of-life and targeting refinements. Most of this is
**automatic on pull** — new fields just appear on the monster definitions,
already reorganized into labeled groups. Two are bug fixes that need nothing
from you. This guide just explains what's new and how to use it.

---

## Step 1 — Pull

GitHub Desktop → **Fetch** → **Pull**. Let Unity recompile; zero errors
expected.

## Step 2 — Estimated DPS readout (automatic)

Click any monster definition in `Assets/Monsters`. At the **top** of its
Inspector is a blue info box: **Estimated DPS** — damage-per-second vs the
Player, King, Structure, and Praise Tower, computed from the damage values ÷
the attack cycle. For telegraphed attackers (Cyclops) the wind-up time is
folded into the cycle, so the number reflects real throughput. It updates live
as you edit the fields below it. Nothing to set up — it's just there to help
you balance.

## Step 3 — Reorganized inspector (automatic)

The monster definition fields are now grouped under clear headers: Identity,
Movement & health, **Damage dealt**, **Attack timing & reach**, **Targeting —
player vs King**, **Targeting — structures**, Economy & weight, then the
per-monster specials (**Goblin / Skeleton / Cyclops**). Same fields, just
easier to find. Your existing values are untouched.

## Step 4 — Skeleton bone-pile fix (automatic)

Two bugs are fixed, no action needed: while a Skeleton is a bone pile it now
(a) **fully hides its health bar** (no more lingering sliver) and (b) is
**completely frozen** — physics is switched off, so it can't creep or be shoved
by other monsters, and towers/your sword ignore it entirely until it revives.

## Step 5 — New targeting knobs (on every monster definition)

Under **Targeting — player vs King**:

- **King Priority Beats Structures** (checkbox) — by default, structure-priority
  outranks the King. Check this and, whenever the King is within **King
  Priority Range**, the King wins over nearby structures too. Use it for a
  monster you want making a beeline for the King once it's close, rather than
  getting distracted by a tower.

- **Keep Target Within Range** (tiles, 0 = off) — this is the fix for "the
  player kept pulling monsters off the King even with a clear path." If a
  monster is within this many tiles of the King (or the structure) it's
  targeting, it **ignores the player entirely, even while being attacked** —
  recent-combat aggro can't peel it off something it's essentially already on.
  Try **`1`** on a monster to stop it from disengaging the King at point-blank
  range. Turn it up if you want monsters to commit harder; leave it 0 for the
  old behavior.

## Step 6 — Save & commit

`Targeting tuning, DPS readout, skeleton bone-pile fix`

---

## ✅ Checkpoint

- [ ] DPS box shows at the top of each monster definition
- [ ] Skeleton bone pile hides its health bar and doesn't move at all
- [ ] King Priority Beats Structures makes a close monster pick the King over a tower
- [ ] Keep Target Within Range stops the player peeling a monster off a point-blank King
