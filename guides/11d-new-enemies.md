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
2. **Faun** — a real new mechanic. A ranged skirmisher that fires a straight
   arrow at its target (Player or a structure) and deals direct hit damage —
   same shape as your Bow, no lingering burn/AoE. It normally just holds its
   firing distance like any settled monster, but a REAL melee hit (the Sword
   or Hammer specifically, not a ranged hit landed up close) makes it
   retreat.

---

## What the code adds (already written & pushed)

- `Combat/Health.cs` — **real weapon-type tracking**, replacing the earlier
  distance-based "was that melee" guess. `TakeDamage` takes a new
  `isMeleeHit` parameter, and a new `LastDamageWasMelee` property (alongside
  the existing `LastDamageFromPlayer`) records it. The Sword and Hammer now
  pass `isMeleeHit: true`; the Bow, Fire Staff, and Faun's own arrow don't
  (defaults false). While in here, also fixed a real (unrelated) bug: the
  Fire Staff's `BurnZone` damage never passed `fromPlayer`, so a monster
  burned by it didn't correctly register as "recently hit by the player" for
  aggro purposes — `BurnZone.Spawn` now takes a `fromPlayer` parameter too.
- `Enemies/MonsterDefinition.cs` — new **Special: Redcap** section
  (`Targets Only Player`), and a new **Special: Faun — ranged attack**
  section (`Uses Ranged Attack`, direct-hit damage, projectile speed, arrow
  color, and the retreat duration).
- `Enemies/MonsterAI.cs`:
  - `PickTarget` gets a `targetsOnlyPlayer` branch — mirrors `targetsOnlyKing`
    exactly, just inverted (always the player, King never considered; if the
    player currently isn't targetable — dead, mid-respawn — it returns
    `null`, which the existing "no objective" handling already deals with by
    just stopping the monster in place).
  - New `UpdateRangedAttack` — Faun's whole behavior: fires on cooldown once
    in range (reusing the existing Attack Range/Attack Interval fields, same
    meaning as every other monster — Attack Range IS the adjustable max
    shoot distance), otherwise calls the same `MoveToward` every monster uses
    to approach/settle/route around walls. Retreating overrides movement to
    step directly away.
  - New `FireRangedAttack` — spawns a `StraightProjectile` (the same one
    your Bow/Fire Staff use) aimed at the target's current position; deals
    direct hit damage on impact, exactly like the Bow.
  - New `OnDamaged` — Faun's retreat trigger, now a real check:
    `health.LastDamageFromPlayer && health.LastDamageWasMelee`.
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
   **Player**, **Structure**, and **King** (Faun's arrow needs to be able to
   hit whichever of these it's aimed at).
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
| Attack Range | **4** — this is Faun's max shoot distance; raise/lower to taste, same field every monster already has |
| Currency Drop | 3 (default) |
| Uses Ranged Attack | **✓** |
| Ranged Damage | **2** |
| Ranged Projectile Speed | **6** |
| Ranged Bolt Color | forest green (e.g. 100, 200, 80) |
| Preferred Min Range | **2** |
| Retreat Seconds | **2** |

*(Player Target Range stays at its 6-tile default, so — like every other
non-King-only monster — it'll also fall back to marching on the King if the
player's out of range; the doc's "Attacks Player/Structures" reads as what
it does once engaged, not a hard restriction. Say the word if you'd rather
it ignore the King entirely too — that'd just be reusing the same "leave a
targeting field at 0" pattern, no new code.)*

### Preferred Min Range — Faun's standing kiting preference

New, following up on a design question you asked: Faun now actively tries to
stay **Preferred Min Range** tiles away from its target at all times (backing
off on its own if something — the crowd, a corner — pushes it closer), not
just when melee-retreating. `2` means it'll hold roughly halfway between
melee range and its 4-tile Attack Range — close enough to reliably land
shots, far enough that a player has to actually close distance to threaten it.

**The corridor-blocking concern** — a Faun trying to hold a stand-off
distance INSIDE a 1-wide maze corridor could otherwise camp mid-corridor and
permanently block every ally behind it from ever reaching the King/player,
since there's no room to step aside in a 1-wide path. Fix: both the kiting
backoff and the melee-retreat reuse the same "ally pressure building up
behind me toward the same target" signal the crowd system already computes
for its "flood like water & surround" behavior (`RearCrowdPressure` /
`Rear Pressure Threshold`, tunable under **Crowd avoidance** on the Monster
prefab). The instant that pressure crosses the threshold, Faun stops trying
to back away and just holds/advances like an ordinary monster — so it only
kites when it actually has room to, and a jammed corridor clears instead of
staying permanently plugged by a Faun that refuses to budge.

---

## Step 5 — Playtest

1. Press **Play**, spawn a couple of each (however you trigger test spawns).
2. **Redcap:** confirm it beelines the player, completely ignoring the King
   even if it's much closer. Wall the player off entirely — the Redcap
   should attack whatever wall/structure is sealing them in, the same way a
   King-rushing monster already breaks through a maze. Confirm it passes
   through Gates without attacking them.
3. **Faun — normal:** let one approach until it's about 4 tiles out (or
   whatever Attack Range you set) — it should stop and start firing arrows
   rather than closing the rest of the distance. Each arrow should deal a
   direct hit, same feel as being shot by the Bow.
4. **Faun — kiting:** walk toward a Faun that's already holding its range —
   once you close to within Preferred Min Range (2 tiles by default) it
   should back away on its own, without needing to hit it. Keep approaching
   and it should keep retreating (still firing) until it runs out of open
   ground to retreat into.
5. **Faun — retreat, real melee check:** walk up and land a **Sword** or
   **Hammer** hit on it — it should immediately start backing away. Then
   test the fixed case: fire a **Bow** or **Fire Staff** shot at it from up
   close (walk right up to it first, then shoot) — it should **NOT**
   retreat, since neither of those ever sets `isMeleeHit`, regardless of
   how close you were standing when it landed.
6. **Faun — corridor test (the design question you asked about):** put a
   Faun and at least one other monster in a 1-wide maze corridor together,
   with the Faun in front. As the group backs up toward the corridor mouth
   trying to reach you or the King, confirm the Faun does NOT permanently
   camp mid-corridor blocking everyone behind it — once an ally is actually
   queued up pressing in from behind, the Faun should stop kiting and just
   hold/advance like a normal monster so the queue keeps moving.
7. Let a Fire Staff burn zone tick on a monster, then check that monster's
   recent-player-combat behavior (e.g. it should stay engaged with you
   rather than wandering back to a structure) — confirms the `BurnZone`
   `fromPlayer` fix actually took.
8. Confirm nothing regressed: existing monsters (Zombie, Goblin, Skeleton,
   Cyclops) still behave exactly as before, and Sword/Hammer/Bow/Fire Staff
   all still damage normally.

---

## ✅ Checkpoint

- [ ] Console has no errors after pulling
- [ ] Monster prefab: **Ranged Hit Layers** = Player + Structure + King,
      **Ranged Bolt Sprite** = Square
- [ ] `Redcap` Monster Definition created with the table above
- [ ] `Faun` Monster Definition created with the table above
- [ ] Redcap rushes the player, ignores the King, breaks through a wall
      only when the player is fully sealed off
- [ ] Faun holds its Attack Range distance and fires arrows that deal direct
      hit damage on impact — no lingering burn/AoE
- [ ] A Sword/Hammer hit makes the Faun retreat; a Bow/Fire Staff hit —
      even landed at point-blank range — does not
- [ ] Faun backs away on its own once you're within Preferred Min Range,
      without needing to hit it
- [ ] A Faun leading a group through a 1-wide corridor stops kiting and
      holds/advances once an ally is actually queued up behind it
- [ ] A monster hit by a Fire Staff burn zone correctly counts as "recently
      hit by the player" (the `BurnZone.fromPlayer` fix)
- [ ] Existing monster types unaffected, all four weapons still deal damage
- [ ] **File ▸ Save Project**, committed & pushed (verified on github.com)

---

## Notes for later — judgment calls worth knowing about

- **"Hit by melee" is now a real check, not a distance guess.** Following up
  on your request, `Health` tracks the actual weapon type on every hit
  (`LastDamageWasMelee`, set from a new `isMeleeHit` parameter on
  `TakeDamage`) — the Sword and Hammer set it true, the Bow and Fire Staff
  don't, so Faun's retreat trigger is exact regardless of how close a ranged
  shot landed. This also means any FUTURE melee weapon should remember to
  pass `isMeleeHit: true`, and any future ranged one shouldn't.
- **Retreating movement bypasses the crowd system — but only while it's
  actually working.** While backing away, Faun moves with a plain direct
  velocity instead of going through `MoveToward`'s separation/give-way/
  stuck-recovery logic. Originally this looked like the Faun was "fighting
  itself" after being hit — jittering in place instead of smoothly fleeing.
  Root cause: the retreat velocity kept getting re-commanded into whatever
  was physically stopping it (a wall behind it, or very commonly just the
  player's own body bumping into it while chasing) every single physics
  step, fighting the collision response each time. Fixed by checking last
  step's actual velocity (`stuckVelocityThreshold`, the same "is this
  monster genuinely moving" signal used elsewhere in `MonsterAI`) — the
  instant a retreat attempt isn't producing real movement, Faun yields to
  `MoveToward` instead of continuing to push into the obstacle. Also fixed
  in the same pass: the retreat direction was computed away from whatever
  `MoveToward`/routing was CURRENTLY resolving as the thing to walk toward
  (which can be a wall it's forced to break through, not the real target) —
  it now always flees the real threat (`currentTarget` — player/King/
  structure), never a routing waypoint.
- **Faun's own generic damage numbers (Player Damage, Structure Damage,
  etc.) are still unused placeholders** — its real damage is **Ranged
  Damage**, dealt directly on arrow impact. The other fields exist only
  because every Monster Definition shares the same schema.
- **Faun can still fall back to the King** if the player goes out of Player
  Target Range (6 tiles, unchanged default) — see the callout in Step 4's
  table if you want that closed off entirely. (Redcap can't — Targets Only
  Player rules the King out unconditionally, that's the whole point of it.)
