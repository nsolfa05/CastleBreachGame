# Guide 10 — Phase 4: Walls, Gates & real pathfinding (§6)

**Goal:** Monsters stop walking in straight lines. They now **route around**
whatever you build, so walls become a real tool — you can funnel monsters down
a corridor, or seal the King in completely and make them chew through. Two new
buildables come with it: the **Wall** (blocks everything) and the **Gate**
(blocks everything except Goblins; you walk through freely).

The rule that ties it together, straight from the design doc: a wall is
**breakable only if there's no path around it**. While any route exists,
monsters walk it and leave your walls alone. Seal every route and they start
smashing whatever is sealing them in.

> **What did NOT change:** monsters still crowd and jostle each other exactly
> as before. Routing only accounts for walls, gates, towers and the King —
> never for other monsters, which still push past each other with plain
> physics. On open ground with nothing in the way, movement is identical to
> before this guide.

> **Update — fixes if you already built Wall/Gate from an earlier pull:**
> playtesting turned up (1) anything sliding along a row of Wall pieces could
> catch on the seam between them, worst at corners — fixed with Edge Radius,
> Step 3, (2) the castle's own border wall had the same seam problem for a
> different reason — fixed with Composite Collider 2D, Step 4.5, and (3) Gate
> needs a proper physics fix, not just "make it a trigger": that let a
> tightly packed crowd physically shove an ordinary monster through one,
> since a trigger has zero resistance regardless of *why* something ends up
> overlapping it. **Step 4 below now uses two dedicated layers instead** —
> if you set Gate to Is Trigger from an earlier pull, undo that; Step 4 walks
> through the replacement.

---

## Step 1 — Pull

GitHub Desktop → **Fetch** → **Pull**. Let Unity recompile; zero Console
errors expected. Three new scripts arrive: `PathGrid`, `Barrier`, and the
routing changes inside `MonsterAI`.

## Step 2 — Add the PathGrid object

This is the brain that knows which tiles are walkable. Nothing routes without
it (and without it monsters simply fall back to the old straight-line
movement, so if something looks wrong later, check this first).

1. In the Hierarchy: **right-click → Create Empty**, name it **`PathGrid`**.
2. **Add Component → Path Grid**.
3. Wire it:
   - **Wall Tilemap** ← the **Wall** tilemap (the child of `Grid`, the same one
     `CastleMapGenerator` uses). This is the castle's own border wall —
     permanent terrain that can never be broken.
   - **Blocking Layers** ← tick **Structure** and **King** for now — **Gate**
     joins this list in Step 4d once that layer exists. **Do NOT tick
     Enemy** — that would make monsters try to route around each other
     instead of crowding, which is exactly what we don't want.
4. Leave the rest at defaults: **Searches Per Frame** `8`, **Rescan Interval**
   `0.25`. (Those are the two performance dials if you ever need them — see
   the note at the end.)
5. **Draw Blocked Tiles** — tick this on for now. While `PathGrid` is selected
   in the Hierarchy you'll see every blocked tile shaded in the Scene view:
   **red** = permanent border wall, **orange** = breakable structure, **blue**
   = gate. Enormously helpful for confirming your maze is what you think it
   is. Untick it once you're happy.

## Step 3 — Build the Wall prefab

1. Hierarchy → **2D Object → Sprites → Square**, name it **`Wall`**.
2. Set it up:
   - **Transform → Scale**: `0.9, 0.9, 1` (a 1×1 building, same as the Pike Tower)
   - **Sprite Renderer → Color**: stone gray
   - **Sprite Renderer → Order in Layer**: `19` (the structure layer order)
   - **Layer** (top-right of the Inspector): **Structure**
3. **Add Component** each of:
   - **Box Collider 2D** → set **Edge Radius** to **`0.05`** (see the callout
     below — don't skip this one)
   - **Health** → **Max Health** `40`
   - **Destroy When Dead**
   - **Barrier** → leave **Is Gate** *unchecked*
4. *(Optional)* drag the **HealthBar** prefab on as a child, Position
   `0, 0.7, 0`, and wire its **Health** ← this wall, **Background** ← its
   Background child, **Fill** ← its Fill child. Skip it if you'd rather not
   have a bar over every wall segment — with a long maze it gets busy.
5. Drag it into `Assets/Prefabs`, then **delete it from the Hierarchy**.

> **Why Barrier matters:** it's what tells routing "this is a wall, not a
> building." Monsters deliberately never pick a Barrier as a target the way
> they'll opportunistically attack a nearby tower — otherwise they'd grind
> down the sides of your corridors just for walking through them, and mazes
> would be pointless. A Barrier only ever becomes a target when there's no
> route at all.

> **Why Edge Radius matters:** place several Wall pieces in a row and each one
> is still a separate collider — even lined up perfectly, Unity's 2D physics
> has a known quirk where something sliding along a row of adjacent square
> colliders can catch on the seam between two of them, like an invisible lip
> exactly where one piece ends and the next begins. Worst at corners, since
> that's where several seams meet in one spot. Edge Radius rounds the
> collider's corners just enough that anything sliding past glides over the
> seam instead of snagging on it. `0.05` is small enough not to visibly change
> the wall's shape.

## Step 4 — Build the Gate prefab

The tricky part of Gate isn't the prefab — it's that it needs to be solid to
most monsters, but not to the player, and not to a Goblin. Unity's physics
can't express "block this layer except these specific members" (every
monster shares the Enemy layer), so this uses two dedicated layers instead of
fighting that: **Gate** collides with ordinary monsters normally (a real solid
wall, not a trigger — see the callout below for why that matters), but not
with the player or with anything on a new **GatePasser** layer, which is where
a Passes-Through-Gates monster gets moved the moment it spawns.

**4a. Create the two layers.** Edit → Project Settings → Tags and Layers →
Layers. Type into two empty **User Layer** slots:
- `Gate`
- `GatePasser`

**4b. Turn off two boxes in the collision matrix.** Edit → Project Settings →
Physics 2D → **Layer Collision Matrix**. This grid only shows each pair of
layers **once**, in whichever row comes first in the list — so the two boxes
you need are in two *different* rows, not both in Gate's:

- In the **Gate** row: uncheck the leftmost box, under the **GatePasser**
  column.
- In the **Player** row (higher up the list): uncheck the box under the
  **Gate** column — that pair lives here instead, since Player comes earlier
  in the layer list than Gate does.

Leave everything else checked, especially Gate × Enemy — that's what makes
Gate solid to ordinary monsters.

**4c. Build the prefab.**

1. In `Assets/Prefabs`, select **`Wall`**, **Ctrl+D** to duplicate, rename the
   copy **`Gate`**.
2. Double-click it (Prefab Mode) and change:
   - **Sprite Renderer → Color**: wooden brown (clearly different from Wall)
   - **Health → Max Health**: `25` (a door is flimsier than a wall)
   - **Barrier → Is Gate**: **check it**
   - **Layer** (top of the Inspector, not the Sorting Layer): change from
     **Structure** to your new **Gate** layer
   - **Box Collider 2D → Is Trigger**: leave **unchecked** — Gate should be
     solid, same as Wall, just on its own layer
3. Exit Prefab Mode.

That **Is Gate** checkbox is the whole Goblin rule: any monster whose
definition has **Passes Through Gates** ticked gets moved onto the
**GatePasser** layer the instant it spawns (that's what Step 4a/4b's layer
exception is for) and its route treats a Gate as open ground. Your `Goblin`
asset already has Passes Through Gates ticked from Guide 08 — no change
needed there.

**4d. Tell PathGrid about the new layer.** Select the **`PathGrid`** object →
**Blocking Layers** → tick **Gate** alongside Structure and King. Miss this
and Gate silently stops counting as an obstacle for routing at all, since it's
no longer on the Structure layer PathGrid was already watching.

> **Why solid-with-an-exception is better than a trigger here:** a trigger has
> zero physical resistance no matter *why* something ends up touching it —
> including a crowd of monsters physically shoving one of their own through
> it from behind, which can happen since monster-vs-monster collision is
> fully on in this project. A real solid collider can't be shoved through by
> crowd pressure the way a trigger can; the layer exception is what lets the
> player and Passes-Through-Gates monsters ignore that solidity specifically,
> without weakening it for everyone else. Damage still works normally either
> way — attacks are a distance check, not a physics collision.

## Step 4.5 — Fix the castle's own border wall too

The border wall isn't built from prefabs like your Wall/Gate — it's a single
Tilemap (from Guide 01) with one **Tilemap Collider 2D** that auto-generates a
collider shape per occupied tile. That has the same seam-between-tiles problem
as Step 3, just via a different component, and Tilemap Collider 2D doesn't
have an Edge Radius field to fix it the same way.

1. In the Hierarchy, find the **`Walls`** Tilemap (child of `Grid`).
2. **Add Component → Rigidbody 2D** → set **Body Type** to **Static** (it's
   fixed terrain, it never moves).
3. **Add Component → Composite Collider 2D**. Leave **Geometry Type** at
   **Polygons**.
4. On the existing **Tilemap Collider 2D**, set **Composite Operation** to
   **Merge** (Unity 6 renamed the old "Used By Composite" checkbox to this
   dropdown — same thing, just pick Merge instead of the default None).

> **Why this is better than Edge Radius here:** Composite Collider 2D merges
> every wall tile's collider into one continuous outline instead of leaving
> one collider per tile — so there's no seam left at all, not just a rounded
> one. It's Unity's standard fix for Tilemap collision snagging, and it fits
> perfectly here since the border wall never breaks apart or changes shape
> (unlike your player-built Walls, which each need their own destructible
> Health and can't share one merged collider).

## Step 5 — Register both in build mode

1. Select **`GameManager`** → **Build Mode Controller** → **Build Options**.
2. Set the list **size to 6** and fill in the two new entries:

| # (hotkey) | Display Name | Prefab | Cost | Footprint |
|---|---|---|---|---|
| 5 | Wall | Wall | 25 | **1, 1** |
| 6 | Gate | Gate | 60 | **1, 1** |

(Costs are a starting suggestion — use the design doc's numbers if it lists
them, and tune from there. Walls want to be cheap enough to build a lot of.)

3. **File → Save Project.** ⚠️ Not just Ctrl+S — the Build Options list lives
   on a component in the scene, and scene changes only reach disk on a save.
   This is the exact step that silently ate a chunk of Guide 08 and Guide 09
   earlier; the Editor and Play Mode both look correct without it because the
   change is live in memory.

## Step 5.5 — Wall Damage (automatic)

Every monster definition has a new **Wall Damage** field (Damage dealt
header), `0` by default meaning "same as Structure Damage." Set it on any
monster to make it chew through your mazes faster or slower than it fights a
tower — independent numbers, since a monster you want to be scary against
towers doesn't have to also be scary against walls, or vice versa. The DPS box
at the top of each monster's Inspector now shows a **vs Wall/Gate** line too.

## Step 5.6 — Corner-hugging & crowd fix (automatic)

Two more things fixed in code, nothing to set up:

- **Waypoint Arrival Radius** (new field, Routing header, default `0.25`) —
  how close a monster must get to a route waypoint before aiming at the next
  one. It was effectively `0.6` before (hardcoded), which let a monster start
  swinging toward the next leg of a route well before it had actually rounded
  a corner, cutting across the inside edge of the wall forming it — worth
  knowing about if corner-snagging still turns up after Steps 3/4.5, since
  it's now a number you can tune down further (or up, if monsters ever seem
  reluctant to advance in a crowd) instead of a bug to report.
- The crowd-avoidance check now always sees every monster regardless of which
  of the two layers Step 4 put it on — a Passes-Through-Gates monster moving
  onto GatePasser was specifically to fool Gate's collider, not to split the
  crowd into two groups that ignore each other.
- **The "clear line to target" shortcut now accounts for a monster's actual
  body width, not just the bare center of the line.** This was the real
  explanation for monsters still snagging right at corners after the fixes
  above: on open ground, a monster skips its planned route and just walks
  straight at its target for a more natural feel — but the check for whether
  that line is actually clear only ever tested tile centers, so a monster
  sitting right next to a corner could see a technically-clear line whose
  edge its own body would still clip, conclude "clear, go straight" every
  single frame, and keep walking straight back into the wall it was already
  touching. It now also checks a margin to each side of the line scaled to
  the monster's Body Scale, so it correctly falls back to its (already
  correct) planned route instead.
### The stuck-on-walls / stuck-on-each-other rework (automatic)

The corner and crowd sticking that kept coming back is fixed by three changes
that work together — all in code and the Monster prefab, nothing to set up,
they arrive when you pull:

- **Monsters now use a round collider instead of a square one, and the
  placeholder body sprite was swapped to match (`Sprites/Circle.png`, the
  same circle already used for the tower-range indicator).** A square
  collider has corners that interlock — against a wall's corner, or against
  another square monster at an angle — and once two corners catch, neither
  body can slide free. A circle has no corners: it always meets a wall or
  another monster on a smooth curved surface, so it slides past instead of
  snagging. This is the single biggest part of the fix; the other two build
  on it (a body that can't slide is one that no amount of steering frees).
  Every monster type (Zombie, Skeleton, Goblin, Cyclops, …) is just the one
  shared `Monster.prefab` recolored per `MonsterDefinition` — there's no
  per-type prefab to redo, so this one swap already covers all of them. Once
  you make real (non-placeholder) art per monster later, just make sure
  whatever you draw actually reads as roughly circular/blob-shaped — a
  sprite that visually looks square again but sits on a circular collider
  would look a little odd (hitbox smaller than the art suggests) even though
  it wouldn't break anything.
- **Omnidirectional crowd separation (new fields, Crowd Avoidance header:
  Separation Radius `0.85`, Separation Strength `0.6`).** The old avoidance
  only ever looked *straight ahead* — it was blind to a monster pressing in
  from the side or behind, which is exactly how pile-ups form, so it could
  never break them. Every monster now feels a gentle push away from *all* its
  close neighbors at once, strongest from the closest, so a bunched-up knot
  spreads itself apart from whatever direction it's densest. Separation
  Strength is how firmly they shove off each other; turn it up if crowds still
  bunch, down if they refuse to gang up on the same target.
- **Stuck recovery now pushes along the route (fields unchanged: Stuck Check
  Interval `0.4`, Stuck Progress Threshold `0.15`, Stuck Escalation Delay
  `0.8`).** For the last-resort cases separation can't solve on its own — a
  body wedged on a wall corner with no neighbor to push off, or two monsters
  dead-locked in a one-wide doorway where their pushes cancel — each monster
  measures whether it's actually moving, and if it genuinely hasn't for Stuck
  Escalation Delay, it leans harder **along its own pathfinding route
  direction** (the way the grid already knows leads out), with a little
  side-to-side jitter to slip off whatever it's caught on, escalating a bit
  more each check until it's free. This is your "push it the way the
  pathfinding says to go" idea. Only ever changes behavior for a monster
  that's measurably stuck — normal movement is untouched.

### The Goblin (and any gate-passer) taking no damage — fixed (automatic)

Making the Goblin pass through gates moved it onto the **GatePasser** layer
(Step 4). The catch: the sword, tower targeting, and tower splash all filter
for the **Enemy** layer, so anything on GatePasser silently fell through every
one of them — the Goblin stopped taking sword damage, and would have been
invisible to towers too. Rather than make you tick a second box on every one
of those fields, the code now automatically folds GatePasser into any
"enemy" layer filter, so gate-passers are hit by everything ordinary monsters
are. Nothing to set up — but if you ever add a new script that filters for the
Enemy layer, route it through `MonsterLayers.IncludeGatePasser` too.

## Step 6 — Playtest

Press **5**, then left-click to lay walls (you keep carrying it, so you can
draw a long line by clicking along it).

- **Routing:** build a wall line across a gate's approach, leaving one gap.
  Monsters should walk to the gap and funnel through rather than pressing into
  the wall. Select `PathGrid` in the Hierarchy to see the blocked tiles shaded
  if anything looks off.
- **Maze, not chew:** build a longer zig-zag corridor. Monsters should walk the
  whole corridor and **never** attack its side walls — as long as a route
  exists, walls are ignored completely.
- **Blockade:** now close that last gap so the King is fully sealed. The next
  monster to reach the seal should stop and start attacking the wall in its
  way. Break one open yourself and watch them immediately re-route through the
  new hole instead of continuing to attack.
- **Gates:** press **6** and plug a gap with a Gate. Ordinary monsters should
  treat it as a wall (route around, or break it if it's the only way through).
  Send a **Goblin** at it — it should walk straight through without stopping.
  You should be able to walk through it too.
- **Crowd pressure at a Gate:** funnel a big group of ordinary monsters (no
  Goblins) toward a Gate so they pile up against it. None should ever end up
  on the far side — a real solid collider can't be shoved through by a crowd
  the way the old trigger version could.
- **Nothing built = nothing changed:** with an empty field, monsters should
  behave exactly as they did before this guide — straight at their target, same
  crowding.
- **No more catching:** walk the player right up against a long wall line,
  then move along it past a corner — should slide smoothly with no snagging.
  Send a group of monsters down a corridor with a corner and watch them round
  it without piling up stuck. Do the same test pressed against the castle's
  own outer border wall.
- **Two monsters, one tight corner:** the real stress test — send several
  monsters down a narrow 1-tile corridor with a turn in it, ideally with a
  gate or dead end nearby that tends to bunch them up. Watch closely at the
  corner: brief hesitation or a small side-to-side shuffle while several try
  to round it at once is fine and expected, but nobody should stay frozen
  there for more than about a second before working free.
- **Goblin takes damage again:** send a Goblin at the player and swing at it —
  it should lose health and die like any other monster. Do the same past an
  Archer or Pike tower — the tower should target and hit it too.
- **Wall Damage:** set it to something high on a test monster and confirm
  walls break noticeably faster than before, without changing how fast that
  monster fights a tower.

## Step 7 — Commit

`Phase 4: walls, gates, pathfinding, and Wall Damage`

Then **push**, and confirm on github.com that the new prefabs actually landed.

---

## ✅ Checkpoint

- [ ] PathGrid object exists, Wall Tilemap wired, Blocking Layers = Structure + King + **Gate** (not Enemy)
- [ ] Wall and Gate prefabs exist with Health, Destroy When Dead, and Barrier
- [ ] Wall and Gate's Box Collider 2D both have Edge Radius `0.05`
- [ ] `Gate` and `GatePasser` layers exist (Tags and Layers)
- [ ] Physics 2D collision matrix: Gate × Player unchecked, Gate × GatePasser unchecked, Gate × Enemy still checked
- [ ] Gate prefab: Layer = Gate, Is Gate checked, Is Trigger **unchecked**; Wall: Layer = Structure, neither checked
- [ ] Both registered in Build Options (hotkeys 5 and 6)
- [ ] Monsters route around walls instead of pressing into them
- [ ] Monsters round a tight corner / a 1-wide corridor without staying frozen
- [ ] Goblins take sword damage and are targeted by towers (not invisible on GatePasser)
- [ ] Monsters never attack walls while any route exists
- [ ] Fully sealing the King makes them break through the seal
- [ ] Breaking a hole makes them immediately re-route through it
- [ ] Goblins AND the player walk through Gates; other monsters don't
- [ ] Standing pressed against a Wall, then trying to move along it, doesn't get you stuck
- [ ] `Walls` Tilemap has Rigidbody 2D (Static) + Composite Collider 2D, and Tilemap Collider 2D → Composite Operation is set to Merge
- [ ] Standing pressed against the castle's own border wall doesn't get you stuck either
- [ ] DPS box shows a vs Wall/Gate line on monster definitions
- [ ] **File → Save Project**, committed & pushed (verified on github.com)

---

## Notes for later

- **Performance dials** (on `PathGrid`, only if you ever need them):
  **Searches Per Frame** caps how many monsters may recalculate a route in the
  same frame — extras keep walking their current route and try next frame, so
  a big wall collapse can't spike the frame time. **Rescan Interval** is how
  often the obstacle grid refreshes; building and breaking already refresh it
  instantly, so this is just a safety net. The grid is small (40×30 = 1200
  tiles), so neither should need touching at this scale.
- **Flying monsters** are already supported by the data: a monster definition
  with **Flies Over Barriers** ticked routes straight over Walls and Gates and
  never needs to break one (towers, the King and the castle's border wall still
  stop it). No monster uses it yet — it's there for when you build one.
- **A wall-smashing monster** — one that attacks walls on sight rather than
  only when trapped — is a deliberate future addition, not something any
  monster does today. The "attack this obstacle" behavior is already its own
  path through the code, so that monster would opt into it rather than needing
  the rule rewritten.
- **Tile Weight Rule (§7.1)** is still deliberately not built — monsters
  crowding and physically bumping is the current (and preferred) behavior.
