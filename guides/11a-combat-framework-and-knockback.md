# Guide 11a — Combat framework & knockback

**Phase 11 (Combat & More) is split into sub-guides you build and test one at a
time**, so nothing stacks on unverified code:

- **11a — Core framework + knockback/stun** ← you are here
- 11b — Weapons (sword rework, bow, hammer, fire staff) + the `V` weapon menu
- 11c — Combat UI (ghost previews, health-bars-on-damage) + gold-on-death
- 11d — New enemies (Faun, Redcap)
- 11e — Oil & Flame tower (+ click-to-select-a-tower system)

This first one lays the **reusable foundation** everything later plugs into: a
single way for any attack — a weapon, a monster, or (later) a tower — to deal
**knockback** and a **stun** on top of its damage, and a single component that
makes the player or a monster physically react to it. After this, each new
weapon/enemy/tower just switches those effects on; no new knockback code.

Everything here defaults **OFF**, exactly as you asked — knockback and stun are
a capability you turn on per attack, not something that happens automatically.

---

## What the code adds (already written & pushed)

Two new shared scripts under `Assets/Scripts/Combat/`:

- **`HitEffects`** — a small set of Inspector fields (Knockback Enabled /
  Strength, Stun Enabled / Duration) that gets embedded in *anything that
  attacks*. The player's sword has one; each Monster Definition now has one.
- **`KnockbackReceiver`** — the component that makes an entity *able to be*
  knocked back / stunned. While a shove or stun is active it briefly takes over
  the body's movement, then hands control straight back — so it never fights the
  normal WASD / monster-AI movement.

Plus wiring: the player's sword and every monster attack now *apply* their
`HitEffects` on hit, a stunned player/monster can't move or attack, and the
sword no longer reaches enemies through a wall (line-of-sight check).

You don't need to read the code — but you **do** need to do the Editor steps
below, because the two prefabs need the new `KnockbackReceiver` component added
and a few values set.

---

## Step 1 — Pull & let Unity compile

1. Pull the latest `main` in your Git tool.
2. Switch to the Unity Editor and let it recompile (watch the bottom-right
   spinner). Open **Window ▸ General ▸ Console** and confirm **no red errors**.
   Two new scripts (`HitEffects`, `KnockbackReceiver`) will appear under
   `Assets/Scripts/Combat/`.

---

## Step 2 — Collapsible inspector sections (new, nothing to build)

Every `[Header]` section on **Monster Definition** assets, the **Monster**
prefab's `MonsterAI` component, **Attack Tower**, and **Player Attack** is now
a **collapsible foldout** instead of a flat wall of fields — click a section's
title to fold it away. This applies automatically; there's nothing to add or
wire, just click around and confirm it looks right:

1. Select the **Cyclops** Monster Definition asset (`Assets/Monsters/Cyclops`).
   You should see foldout headers like **Damage dealt (§7.3)**, **Targeting —
   player vs King**, **Knockback & stun dealt (Guide 11)**, etc. — click one to
   collapse/expand it.
2. Select the **Monster** prefab and look at its `MonsterAI` component — same
   thing, now with foldouts for **Crowd avoidance**, **Attack slots**,
   **Routing around walls**, etc.
3. A section you collapse **stays collapsed** while you keep working (even if
   you click to a different asset and back) — it only resets if you close and
   reopen Unity. Handy for hiding the pathing tunables while you're focused on
   combat, for instance.

Any *new* field or `[Header]` added later — including everything the rest of
Guide 11 adds — automatically lands in the right foldout (or a new one) with no
extra setup on your end.

---

## Step 3 — Add `KnockbackReceiver` to the Player

1. In the **Hierarchy**, select your **Player** object (the knight — the one with
   `Player Movement` and `Player Attack` on it).
2. In the **Inspector**, click **Add Component**, type **Knockback Receiver**,
   and add it.
3. Set its fields:
   - **Weight → `6`** (the player is heavy — design doc tile weight 6 — so small
     enemies barely nudge them).
   - Leave **Knockback Resistance `0`**, **Knockback Duration `0.18`**,
     **Knockback Scale `1`** for now (tune later by feel).

> The player already has a Rigidbody 2D (Player Movement requires one), so the
> receiver just uses that — nothing else to wire.

---

## Step 4 — Add `KnockbackReceiver` to the Monster prefab

1. In the **Project** window, open `Assets/Prefabs/`, and **double-click the
   `Monster` prefab** to open it in Prefab Mode (or select it and use the
   Inspector).
2. **Add Component ▸ Knockback Receiver**.
3. **Leave Weight at its default** — you don't set it here. `MonsterAI` copies
   each monster's **Tile Weight** (from its Monster Definition) into the receiver
   automatically at spawn, so a Zombie (weight 2) gets shoved far and a Cyclops
   (weight 6) barely moves, all from data you already set.
4. Save the prefab (**Ctrl/Cmd+S**, then **File ▸ Save Project**).

> If you skip this, monsters simply won't get knocked back (everything no-ops
> safely) — but the sword's knockback is the main thing you'll want to see, so
> add it.

---

## Step 5 — Turn the sword's knockback + stun on

The sword is the first weapon to use the framework (the full weapon rework is
11b; this is just the knockback/stun/wall-block part).

1. Select the **Player** again. Find the **Player Attack** component.
2. Set **Obstruction Layers** → tick **Structure** and **King**. (This is the
   line-of-sight block: an enemy with a wall/tower/King between it and you is no
   longer hit *through* the wall — only the part of the swing on your side of the
   obstacle lands.)
3. Expand **Sword Effects** and set:
   - **Knockback Enabled → ✔**, **Knockback Strength → `12`** (a starting value —
     raise for a bigger shove).
   - **Stun Enabled → ✔**, **Stun Duration → `0.25`** (the "brief stun that stops
     them moving forward" you described).

---

## Step 6 — Give the "large enemy" a knockback + stun vs the player

Per your brief, a *big* enemy hitting the player should knock back **and** stun;
a small one just nudges. Let's set that on the **Cyclops** (the big one) and give
the **Zombie** a small nudge.

1. In **Project**, open `Assets/Monsters/` and select **`Cyclops`**.
2. Find the **Knockback & Stun Dealt (Guide 11)** foldout → **Attack Effects**:
   - **Knockback Enabled → ✔**, **Knockback Strength → `20`**.
   - **Stun Enabled → ✔**, **Stun Duration → `0.6`**.
3. Select **`Zombie`**. Under **Attack Effects**:
   - **Knockback Enabled → ✔**, **Knockback Strength → `6`** (a slight nudge).
   - **Stun Enabled → ✗** (leave stun off for small enemies).
4. Leave the others (Armored Zombie, Skeleton, Goblin) **off** for now, or tune
   later — they're all off by default.

> These push the *player* away from the monster (the player has the receiver).
> The King and structures don't have receivers, so monster attacks never knock
> *them* around — exactly as intended.

### Stun Resistance — the OTHER side of stun (new)

Step 5 gave the sword a Stun Duration, and Step 6 gave the Cyclops one too — but
until now, every enemy took the exact same stun length from the sword, no matter
how big it was. Each Monster Definition now has its own **Stun Resistance**
(0–1) under the new **Knockback & Stun RECEIVED (Guide 11)** foldout — `0` =
fully stunnable, `1` = stun-immune, `0.5` = stuns on it last half as long. This
is separate from Tile Weight (which only affects knockback *distance*, not
stun).

1. Select **`Cyclops`** → **Knockback & Stun Received (Guide 11)** →
   **Stun Resistance → `0.5`** (a big monster shrugs off half the stun the sword
   would otherwise give it).
2. Select **`Zombie`** → **Stun Resistance → `0`** (leave it fully stunnable —
   the default).
3. Tune any others to taste; `0` (no resistance) is the default for all of them.

**Save Project.**

---

## Step 7 — Playtest

1. Press **Play**. Send a wave (however you normally spawn — the Test controls or
   your wave setup).
2. **Sword knockback:** swing (Spacebar) into a clump of Zombies — they should
   get shoved back a bit and briefly freeze, not just take damage in place.
   Bigger `Knockback Strength` = bigger shove; the Cyclops should barely move
   (weight 6) while Zombies fly (weight 2).
3. **Sword wall-block:** stand on one side of a wall with an enemy just on the
   other side, and swing so the arc overlaps the wall. The enemy across the wall
   should take **no** damage; an enemy on *your* side of the same swing still
   does. (Toggle **Obstruction Layers** empty to see the difference — it'll hit
   through walls again.)
4. **Getting hit:** let a **Cyclops** land a slam on you — you should get shoved
   away and frozen for ~0.6s (can't move or swing), then recover. A **Zombie**
   hit should give a small nudge and no freeze.
5. **Stun Resistance:** swing the sword at a Cyclops vs a Zombie with the same
   Sword Effects Stun Duration — the Cyclops should freeze for noticeably less
   time (half, with the `0.5` from Step 6) than the Zombie.
6. Tune to taste: `Knockback Strength` per attack for shove distance, `Stun
   Duration` for freeze length, `Stun Resistance` per monster for how much of
   that they shrug off, the player's `Knockback Scale`/`Weight` for how the
   player takes hits overall.

---

## ✅ Checkpoint

- [ ] Console has no errors after pulling; `HitEffects` + `KnockbackReceiver`
      exist under `Assets/Scripts/Combat/`
- [ ] Monster Definitions / the Monster prefab's `MonsterAI` / Attack Tower /
      Player Attack all show **collapsible foldout sections** instead of a flat
      field list, and a collapsed section stays collapsed as you work
- [ ] Player has a **Knockback Receiver** (Weight 6)
- [ ] Monster prefab has a **Knockback Receiver** (Weight left default —
      auto-filled from Tile Weight)
- [ ] Player Attack: **Obstruction Layers** = Structure + King; **Sword Effects**
      knockback + stun enabled
- [ ] Cyclops (and any others you want) has **Attack Effects** knockback/stun set;
      small enemies a lighter nudge or nothing
- [ ] Cyclops has **Stun Resistance `0.5`**; Zombie left at `0`
- [ ] Sword shoves + briefly freezes enemies; heavier enemies resist more
- [ ] A stun-resistant enemy (Cyclops) freezes for noticeably less time than a
      non-resistant one (Zombie) from the same sword hit
- [ ] Sword no longer hits enemies through a wall
- [ ] A big enemy's hit knocks back + stuns the player; a small enemy just nudges
- [ ] **File ▸ Save Project**, committed & pushed (verified on github.com)

---

## Notes for later (so nothing here surprises 11b–11e)

- **This is the shared spine.** 11b's bow/hammer/fire staff, and 11e's Oil &
  Flame tower, all reuse `HitEffects` + `KnockbackReceiver` — e.g. the Catapult
  will get an `Attack Effects` block for its "splash knocks enemies outward from
  the impact centre" (knockback already radiates from a source point, so that's
  just turning it on).
- **Weight = knockback mass AND tile weight.** They're the same number on
  purpose. If the §7.1 tile-weight stacking rule is ever built, this stays
  consistent with it.
- **The sword's wall-block is the cheap line-of-sight version** we agreed on. If
  you later want the fancier "expanding arc that physically stops on the wall"
  for visuals, that drops in at the sword without touching this framework.
- **On removing redundant fields:** you asked me to delete anything that's a
  copy of something else already in an enemy's inspector. I went through
  `MonsterAI` and `MonsterDefinition` carefully and didn't find a genuine
  duplicate — every value plays a distinct role, and the handful of `MonsterAI`
  fields that LOOK adjacent to Definition data (e.g. `Structure Layers`, `Body`)
  are scene-wiring references, not copies of a stat. Point me at the specific
  field(s) you had in mind and I'll remove them.
- **New foldout editors are additive, not a rewrite.** `FoldoutHeaderEditor`
  (`Assets/Scripts/Editor/`) is a small reusable base class; `MonsterDefinitionEditor`
  and `AttackTowerEditor` were converted to use it (keeping their DPS summary
  boxes), and `MonsterAIEditor`/`PlayerAttackEditor` are new. Any future script
  with `[Header]` sections can opt in the same way — just inherit
  `FoldoutHeaderEditor` instead of `Editor`.
