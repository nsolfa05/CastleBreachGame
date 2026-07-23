# Guide 08 — Phase 2: The monster roster (§7.3)

**Goal:** All five design-doc monsters exist and fight: **Armored Zombie**,
**Skeleton** (dies into an invulnerable bone pile, then revives!), **Goblin**
(ignores you completely and beelines the King), and **Cyclops** (huge, slow,
smashes structures first). Thanks to Guide 07, this whole guide is *creating
four asset files and typing numbers* — no code, no new prefabs.

---

## Step 1 — Pull

GitHub Desktop → **Fetch** → **Pull** (in case anything moved), let Unity
compile, zero errors expected.

## Step 2 — Create the four definitions

For each one: right-click in `Assets/Monsters` → **Create → Castle Breach →
Monster Definition**, name it, then set ONLY the fields listed (everything
not listed keeps its default). Colors are placeholder suggestions — pick
what reads clearly against the grass for you.

### `ZombieArmored`
| Field | Value |
|---|---|
| Display Name | Armored Zombie |
| Body Color | steel gray-green (e.g. 77, 102, 89) |
| Body Scale | 0.9 |
| Max Health | **20** |
| Structure Damage | **5** |
| Currency Drop | **5** |

*(Speed 4, player damage 3/1.5s, 6-tile aggro — all inherited defaults,
matching the doc.)*

### `Skeleton`
| Field | Value |
|---|---|
| Display Name | Skeleton |
| Body Color | bone white (235, 235, 217) |
| Body Scale | 0.8 |
| Move Speed | **5** |
| Max Health | **6** |
| Player Damage | **2** |
| Player Target Range | **8** |
| Currency Drop | **4** |
| Extra Lives | **1** |
| Revive Delay Seconds | 6 (default) |

### `Goblin`
| Field | Value |
|---|---|
| Display Name | Goblin |
| Body Color | orange (230, 140, 38) |
| Body Scale | 0.75 |
| Move Speed | **8** |
| Max Health | **6** |
| Player Damage | **0** |
| Player Target Range | **0** (never targets the player) |
| Attack Interval | **1.0** |
| Structure Damage | **2** |
| Praise Tower Damage | **4** |
| Currency Drop | **2** |
| Targets Only King | **✓** |
| Praise Tower Lure Range | **4** |
| Passes Through Gates | **✓** *(matters once player-built gates exist)* |

### `Cyclops`
| Field | Value |
|---|---|
| Display Name | Cyclops |
| Body Color | bruised purple (140, 64, 115) |
| Body Scale | **1.7** (the 2×2 hitbox) |
| Move Speed | **2.5** |
| Max Health | **50** |
| Player Damage | **8** |
| Player Target Range | **2** |
| Attack Interval | **2.5** |
| Attack Range | **1.8** |
| Structure Damage | **15** |
| Currency Drop | **15** |
| Tile Weight | **6** |
| Prioritizes Structures | **✓** |
| Structure Priority Range | **6** |

> Doc notes we're simplifying for now: the Cyclops's fancy 2.5s "lock-on
> zone" attack is approximated by its slow 2.5s attack interval, and the
> Goblin's lure-range "unobstructed" check is skipped until pathfinding
> exists. Both are recorded for later refinement.

## Step 3 — Build a showcase wave set

Select **`WaveSpawner`**, expand **Waves**, and reshape it (set the list's
**size** to 4, then fill in). Suggested progression:

- **Wave 1 — "Scouts":** one group: Zombie × 4, West gate.
- **Wave 2 — "Armor":** Zombie × 3 West **+** ZombieArmored × 2 East.
- **Wave 3 — "Bones & Burglars":** Skeleton × 4 North **+** Goblin × 3
  South with **Start Delay 5** (they rush in while your hands are full).
- **Wave 4 — "The Big Guy":** Cyclops × 1 West **+** Zombie × 4 West with
  **Start Delay 3**.

**Ctrl+S** when done.

## Step 4 — Playtest each personality

- **Armored Zombie**: takes twice the sword hits (10 swings!); watch it chew
  through a tower noticeably faster than a regular zombie.
- **Skeleton**: aggros from farther away and moves quicker. Kill it — it
  squashes into a pale pile. Try hitting the pile: nothing (invulnerable,
  and towers ignore it too). Six seconds later it pops back up at full
  health. It only pays its 4 gold when killed the *second* time.
- **Goblin**: fast, orange, and completely uninterested in you — stand in
  its path and it runs right past toward the King. Kill them early; the
  King eats their damage otherwise. (Their Praise-the-King-Tower obsession
  activates next guide, when that tower exists.)
- **Cyclops**: huge and slow. Place a tower near its entry path and watch it
  *detour to destroy the tower first* (structures within 6 tiles outrank
  everything). 15 damage per swing wrecks a 50 HP tower in 4 hits. It only
  bothers with you if you're within 2 tiles.
- HUD sanity: wave counter reads `/ 4` now; bigger drops (15g) from the Cyclops.

## Step 5 — Commit

`Phase 2: full monster roster — armored, skeleton, goblin, cyclops`

---

## ✅ Checkpoint

- [ ] Five definition assets in `Assets/Monsters`
- [ ] Four showcase waves configured
- [ ] Skeleton revives once; pile is invulnerable
- [ ] Goblin ignores the player entirely
- [ ] Cyclops detours to smash structures within 6 tiles
- [ ] Committed & pushed

**Next:** [Guide 09 — Easy structures](09-easy-structures.md): Pike Tower,
Catapult, and the gold-printing Praise the King Tower.
