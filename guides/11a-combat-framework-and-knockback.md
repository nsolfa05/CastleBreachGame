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

## Step 2 — Add `KnockbackReceiver` to the Player

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

## Step 3 — Add `KnockbackReceiver` to the Monster prefab

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

## Step 4 — Turn the sword's knockback + stun on

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

## Step 5 — Give the "large enemy" a knockback + stun vs the player

Per your brief, a *big* enemy hitting the player should knock back **and** stun;
a small one just nudges. Let's set that on the **Cyclops** (the big one) and give
the **Zombie** a small nudge.

1. In **Project**, open `Assets/Monsters/` and select **`Cyclops`**.
2. Find the new **Knockback & Stun Dealt (Guide 11)** header → **Attack Effects**:
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

**Save Project.**

---

## Step 6 — Playtest

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
5. Tune to taste: `Knockback Strength` per attack for shove distance, `Stun
   Duration` for freeze length, the player's `Knockback Scale`/`Weight` for how
   the player takes hits overall.

---

## ✅ Checkpoint

- [ ] Console has no errors after pulling; `HitEffects` + `KnockbackReceiver`
      exist under `Assets/Scripts/Combat/`
- [ ] Player has a **Knockback Receiver** (Weight 6)
- [ ] Monster prefab has a **Knockback Receiver** (Weight left default —
      auto-filled from Tile Weight)
- [ ] Player Attack: **Obstruction Layers** = Structure + King; **Sword Effects**
      knockback + stun enabled
- [ ] Cyclops (and any others you want) has **Attack Effects** knockback/stun set;
      small enemies a lighter nudge or nothing
- [ ] Sword shoves + briefly freezes enemies; heavier enemies resist more
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
