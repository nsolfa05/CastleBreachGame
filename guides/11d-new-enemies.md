# Guide 11d — New enemies: Faun & Redcap

- 11a — Core framework + knockback/stun ✅
- 11b — Weapons (sword rework, bow, hammer, fire staff) + the `V` weapon menu ✅
- 11c — Combat UI (health-bars-on-damage, gold-on-death, selection-mode
  indicator, death effect) ✅
- **11d — New enemies (Faun, Redcap)** ← you are here
- 11e — Oil & Flame tower (+ click-to-select-a-tower system)

Pulled straight from your ideation doc (the sections weren't in the repo, so
I went back to the two original files). Two very different monsters:

1. **Redcap** — turned out to need almost no new code. It's "an alternative
   Goblin" that rushes the **player** instead of the King, ignoring the King
   entirely, and only attacks a wall/structure when forced to (blocked off).
   That's the exact mirror of Goblin's existing `Targets Only King` — one new
   `Targets Only Player` flag, and everything else (routing, wall-breaking
   when sealed off, Gates-as-open-tiles) is the same generic system every
   monster already uses.
2. **Faun** — a real new mechanic. A ranged skirmisher that fires a bolt at
   its target (Player or a structure) which leaves a damaging burn zone on
   impact — no direct hit damage, same shape as your Fire Staff. It normally
   just holds its firing distance like any settled monster, but if you land a
   close-range hit on it, it starts retreating; it also steps out of the burn
   zone it just created instead of standing in its own fire.

---

## What the code adds (already written & pushed)

- `Enemies/MonsterDefinition.cs` — new **Special: Redcap** section
  (`Targets Only Player`), and a new **Special: Faun — ranged attack**
  section (`Uses Ranged Attack`, projectile speed, burn damage/tick/duration/
  radius/color, and the two retreat tunables below).
- `Enemies/MonsterAI.cs`:
  - `PickTarget` gets a `targetsOnlyPlayer` branch — mirrors `targetsOnlyKing`
    exactly, just inverted (always the player, King never considered; if the
    player currently isn't targetable — dead, mid-respawn — it returns
    `null`, which the existing "no objective" handling already deals with by
    just stopping the monster in place).
  - New `UpdateRangedAttack` — Faun's whole behavior: fires on cooldown once
    in range (reusing the existing Attack Range/Attack Interval fields, same
    meaning as every other monster), otherwise calls the same `MoveToward`
    every monster uses to approach/settle/route around walls. Retreating or
    standing in its own burn zone overrides movement to step directly away.
  - New `FireRangedAttack` — spawns a `StraightProjectile` (the same one
    your Bow/Fire Staff use) aimed at the target's current position; on
    impact it spawns a `BurnZone` (same one the Fire Staff uses) instead of
    dealing direct damage.
  - New `OnDamaged` — Faun's retreat trigger. See the judgment call below on
    how "was I hit by melee" is actually detected.
  - Two new **Scene wiring** fields (set once on the shared Monster prefab,
    like `Structure Layers` already is): **Ranged Hit Layers** and
    **Ranged Bolt Sprite**.

---

## Step 1 — Pull & let Unity compile

Pull the latest `main`, let Unity recompile, confirm no red errors in the
Console.

---

## Step 2 — Wire the two new Monster prefab fields

1. **Project ▸ Assets/Prefabs**, double-click **`Monster`** to open it in
   Prefab Mode.
2. Find **Monster AI ▸ Scene wiring** → **Ranged Hit Layers** → tick
   **Player**, **Structure**, and **King** (Faun's burn zone needs to be
   able to damage whichever of these it's aimed at).
3. **Ranged Bolt Sprite** → `Assets/Sprites/Square`.
4. Save the prefab (**Ctrl/Cmd+S**) and **File ▸ Save Project**.

This only matters for a monster with **Uses Ranged Attack** on — safe to
leave alone for every monster that doesn't use it.

---

## Step 3 — Create the Redcap

**Project ▸ Assets/Monsters** → right-click → **Create ▸ Castle Breach ▸
Monster Definition**, name it `Redcap`, set:

| Field | Value |
|---|---|
| Display Name | Redcap |
| Body Color | blood red (e.g. 170, 35, 35) |
| Body Scale | 0.75 |
| Move Speed | **8** |
| Max Health | **6** |
| Player Damage | **3** |
| Structure Damage | **2** |
| Attack Interval | **1.0** |
| Currency Drop | **2** |
| Targets Only Player | **✓** |
| Passes Through Gates | **✓** |

*(Everything else — King Damage, Praise Tower Damage, Wall Damage,
targeting ranges, etc. — keeps its default; none of it applies once Targets
Only Player is on.)*

---

## Step 4 — Create the Faun

Same process, name it `Faun`, set:

| Field | Value |
|---|---|
| Display Name | Faun |
| Body Color | forest green (e.g. 100, 160, 75) |
| Body Scale | 0.85 (default) |
| Move Speed | 4 (default) |
| Max Health | **8** |
| Attack Range | **4** |
| Currency Drop | 3 (default) |
| Uses Ranged Attack | **✓** |
| Ranged Projectile Speed | **6** |
| Ranged Burn Damage Per Tick | **1** |
| Ranged Burn Tick Interval | **1** |
| Ranged Burn Duration Seconds | **4** |
| Ranged Burn Radius | **1** |
| Ranged Burn Color | forest green, semi-transparent (e.g. 100, 200, 80, alpha ~0.5) |
| Retreat Trigger Distance | **1.5** |
| Retreat Seconds | **2** |

*(Player Target Range stays at its 6-tile default, so — like every other
non-King-only monster — it'll also fall back to marching on the King if the
player's out of range; the doc's "Attacks Player/Structures" reads as what
it does once engaged, not a hard restriction. Say the word if you'd rather
it ignore the King entirely too — that'd just be reusing the same "leave a
targeting field at 0" pattern, no new code.)*

---

## Step 5 — Playtest

1. Press **Play**, spawn a couple of each (however you trigger test spawns).
2. **Redcap:** confirm it beelines the player, completely ignoring the King
   even if it's much closer. Wall the player off entirely — the Redcap
   should attack whatever wall/structure is sealing them in, the same way a
   King-rushing monster already breaks through a maze. Confirm it passes
   through Gates without attacking them.
3. **Faun — normal:** let one approach until it's about 4 tiles out — it
   should stop and start firing bolts rather than closing the rest of the
   distance. A bolt should leave a small burn patch wherever it lands
   (on you or on a structure), ticking damage for a few seconds.
4. **Faun — retreat:** walk up and land a **Sword** hit on it — it should
   immediately start backing away. Confirm a **Bow**/**Fire Staff** hit from
   farther out does **not** trigger the retreat (only a close-range hit
   should — see the judgment call below on how this is actually detected).
5. **Faun — self-avoidance:** watch a Faun that's cornered/close to its own
   target — if its own burn patch ends up under its feet, it should step out
   of it rather than standing in its own fire.
6. Confirm nothing regressed: existing monsters (Zombie, Goblin, Skeleton,
   Cyclops) still behave exactly as before.

---

## ✅ Checkpoint

- [ ] Console has no errors after pulling
- [ ] Monster prefab: **Ranged Hit Layers** = Player + Structure + King,
      **Ranged Bolt Sprite** = Square
- [ ] `Redcap` Monster Definition created with the table above
- [ ] `Faun` Monster Definition created with the table above
- [ ] Redcap rushes the player, ignores the King, breaks through a wall
      only when the player is fully sealed off
- [ ] Faun holds its ~4-tile range and fires bolts that leave a damaging
      burn patch on impact
- [ ] A melee hit makes the Faun retreat; a ranged hit from farther away
      does not
- [ ] Faun steps out of its own burn patch rather than standing in it
- [ ] Existing monster types unaffected
- [ ] **File ▸ Save Project**, committed & pushed (verified on github.com)

---

## Notes for later — judgment calls worth knowing about

- **"Hit by melee" is a distance proxy, not a real weapon-type check.**
  `Health.TakeDamage` doesn't currently know or care which weapon dealt the
  damage — only whether it came from the player. So Faun's retreat trigger
  (`OnDamaged`) just checks "is the player within Retreat Trigger Distance
  right now" at the moment damage lands. In practice this reads as "melee"
  correctly almost always (only the Sword/Hammer can realistically land a
  hit that close), but it's theoretically foolable — e.g. if the player
  fires a Bow arrow and then sprints into melee range before it lands, that
  ranged hit would register as if it were melee. Flagging this because the
  real fix (tagging every attack with a weapon type, threading it through
  `HitEffects`/`Health.TakeDamage`) is a much bigger, cross-cutting change
  than one monster's kiting behavior justifies right now — say the word if
  you want that built properly later.
- **Retreating/zone-avoiding movement bypasses the crowd system.** While a
  Faun is backing away or stepping out of its own burn patch, it moves with
  a plain direct velocity instead of going through `MoveToward`'s
  separation/give-way/stuck-recovery logic — so a retreating Faun could
  clip through an ally for that brief window. Same category of tradeoff as
  the Cyclops's telegraph movement, not a new kind of shortcut.
- **Faun's own damage numbers (Player Damage, Structure Damage, etc.) are
  unused placeholders.** All of Faun's damage comes from the burn zone
  (`Ranged Burn Damage Per Tick`), never a direct hit — exactly like your
  Fire Staff. The other damage fields exist because every Monster
  Definition shares the same schema, not because Faun reads them.
- **Redcap can still fall back to the King** if the player goes out of
  Player Target Range (6 tiles, unchanged default) — see the callout in
  Step 4's table if you want that closed off entirely.
