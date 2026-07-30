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
- **Give way — front-rank monsters slide aside to let allies in (new
  fields, Crowd Avoidance header: Give Way Radius `1.1`, Give Way Strength
  `0.6`).** The remaining case: a monster that has *arrived* and is attacking
  the King (or a tower, or a wall) sits in the one reachable spot while allies
  pile up uselessly behind it, unable to reach the same target. Now, whenever
  a monster in attack range notices an ally queued directly behind it heading
  for the *same* target, it slides sideways **along the target's edge** to open
  the front spot — while still pressing into the target, so it keeps attacking
  the whole time and never drifts out of range or loses its target. Repeated
  down the line, a stack fans out into a balanced arc around the target
  (chosen toward the emptier side) instead of a single-file queue. A lone
  attacker with nobody behind it never moves. This replaced the old stop-gap
  where an arrived monster relied on stuck-recovery to shuffle — that also
  made *lone* attackers fidget pointlessly and could nudge them off-target;
  give-way is the precise version: it only slides when an ally actually needs
  the room, and only ever tangentially, never backward. Turn Give Way Strength
  down if the shuffling reads as too eager, or to `0` to switch it off.
- **Yield at a 1-wide gap — the one behind eases back so the one in front
  funnels through (new fields, Crowd Avoidance header: Yield Probe Radius
  `0.9`, Yield Back Strength `0.7`, Yield Hold Seconds `0.3`).** Give-way and
  the sideways stuck-push both assume there's *room to the side* to spread
  into — which there isn't at the mouth of a single-tile gap, where the only
  sideways direction is wall. So two monsters trying to squeeze through the
  same gap at once used to just wedge it. Now, when a *travelling* monster is
  physically jammed directly behind an ally that's ahead of it and closer to
  the goal, the one behind briefly **eases backward** (not sideways) to open
  the mouth, lets the leader funnel in, then follows — a clump sorts itself
  into single file. It's a *self*-yield rather than the front monster shoving
  the back one: each monster only ever drives its own movement, which physics
  resolves far more cleanly than one body flinging another (and it looks the
  same — the one behind backs off). Only ever triggers while genuinely stuck
  at that instant, so in the open, monsters still pack into a crowd instead of
  politely trailing each other. Turn Yield Back Strength up if they're still
  wedging a gap, or Yield Probe Radius to `0` to switch it off.

### Attack slots — monsters claim a spot around a target (automatic)

The force behaviors above all react to crowding *after* it happens, and can
only spread monsters into space that exists nearby. That's why a target boxed
into a tight alcove still jammed no matter how they were tuned: there was
simply nowhere to the side to shuffle into. Attack slots fix it from the other
end. Each target (King, tower, or a wall being broken through) now offers a set
of **slots** — the actual walkable tiles around it a monster could stand on and
still land a hit — and each monster closing in claims one, walks to *it*, and
holds it until it dies or switches targets. A crowd fans into a ring instead of
piling onto one point, and when every slot is taken the extra monsters just
queue (handled by the same force behaviors) instead of grinding against space
that isn't there.

Why this is the real fix and not another force tweak: the slots are generated
from the *actual* map around the target, live. A wall built or broken next to
the King regenerates them automatically (they're cached against the same
version counter `PathGrid` already uses). So a target with one narrow opening
correctly offers only the 2–3 slots that opening allows — the monsters that
fit attack, the rest wait — which is exactly the behavior the alcove
screenshots were missing.

Nothing to set up — it's on by default, and the two knobs live on the Monster
prefab under a new **Attack slots** header:
- **Use Attack Slots** (`on`) — the master switch; untick to fall back to the
  old "everyone approaches the target's surface" behavior.
- **Slot Claim Distance** (`4`) — how close a monster gets before it claims a
  slot and peels off to it. Short on purpose, so it streams in normally from
  range and only fans out for the final approach.

The design already generalizes to things that don't exist yet — a slot is any
walkable tile within a monster's **Attack Range** with a clear line to the
target, so a future ranged type would automatically get a wide outer ring of
slots (and never generate ones that would mean shooting through a wall), while
melee keeps its tight inner ring — but nothing extra was built for that; it
falls out of the same function.

**To watch it work:** select `PathGrid` in the Hierarchy and tick its new
**Draw Attack Slots** debug box. During play you'll see each target's slots in
the Scene view — green = free, red = claimed. This is the fastest way to
confirm a boxed-in target really is offering fewer slots (rather than guessing).

### A slot in a doorway blocking everyone behind it — fixed (slot migration)

Real bug, playtesting caught it (and the Draw Attack Slots view made it
obvious): slots are generated purely from "is this tile walkable, in range, and
in sight of the target" — with no idea whether a given tile also happens to be
the *only* way through a narrow gap. The first monster to arrive claims exactly
that chokepoint tile and holds it, while everyone behind has claimed free slots
on the far side they can't path to, because the blocker is in the doorway.

The fix is a deterministic **slot migration**, which is exactly the behavior
you described. When a settled monster detects an ally genuinely queued behind
it (sharing its target, not yet arrived, directly behind it toward the target),
it tries to **claim a different free slot and move there** — vacating its
current tile so the blocked ally can take it — while still attacking the same
target from its new spot. Crucially: **if there is no other free slot, it does
nothing and stays put**, since the best it can do is keep attacking and there's
nowhere better to go. It claims the new slot *before* releasing the old one, so
it's never briefly slot-less, and a short **Slot Migrate Cooldown** (`0.6`s,
new field under Attack Slots, replacing the old release-cooldown) stops it
hopping around the ring every frame. Watch it with Draw Attack Slots on: the
red (claimed) squares shuffle as blockers relocate and the ones behind fill in
the freed tiles. Nothing to set up.

### Monsters now walk around to their slot, and break only the sealing wall

Two related routing fixes, both automatic:

- **A monster now paths to its actual slot tile, not just the objective.**
  Before, a monster's route always led to the King itself; a slot only
  redirected the *final* step, and only if it already had a clear line to it.
  So a monster assigned a slot on the far side of the King had no idea how to
  walk *around* to it — it just pressed the near face, and the "migrate to a
  free slot" behavior could hand it a far slot it could never actually reach.
  Now it routes to the slot's tile directly, with the King treated as solid, so
  it circles around to a far slot when there's a lane. If the slot turns out to
  be sealed off (no lane — e.g. the far side of a fully-walled King), it gives
  the claim up and falls back to breaking in, so it never clings to a spot it
  can't get to.
- **When walls must be broken, monsters break the one sealing the objective —
  not the nearest wall to them.** This was the bug where a walled-in King made
  monsters chew whatever wall they were standing next to instead of following
  the maze. The break target is now chosen as the breakable wall **closest to
  the King** (the one actually sealing it in), so monsters route through the
  maze to the real seal and leave the corridor walls alone. As before, this
  only ever happens when there's genuinely no open route — build a maze with a
  path through it and they walk the path, never touching its walls.

One honest consequence, matching how it should feel: if you seal the King so
only a couple of tiles around it are reachable through one opening, then only a
couple of monsters can attack *from open ground* — walling the King well is
*supposed* to slow them down. But a horde no longer just queues forever behind
that one gap: see the next section, where overflow monsters break the ring walls
to make more room.

### Debug gizmos actually showing up now, plus a new targeting view (fixed)

If **Draw Blocked Tiles** or **Draw Attack Slots** never seemed to draw
anything, this was it: both used to only draw while `PathGrid` itself was
selected in the Hierarchy, silently — nothing told you that was the
requirement. They now draw unconditionally, so you'll see them any time the
Scene view is open, no clicking required. Two things are still on you, since
they're plain Editor state outside anything a script controls: you need to
actually be in **Play Mode** (nothing is scanned yet in Edit Mode, so there's
truly nothing to draw), and the **Scene view's own Gizmos toggle** needs to be
on (top-right of the Scene view window — easy to have off without noticing).

A third debug toggle joins the other two: **Draw Targeting Debug**. With it
on, every monster draws a thin line to whatever it's currently aiming at —
**yellow** to the player, **cyan** to anything else (King, tower, wall) — plus
a brief **red flash** right at the point of impact the instant a hit actually
lands. Useful for confirming at a glance who's actually fighting what, rather
than guessing from HP bars ticking down.

### Jitter once a monster arrives — fixed (automatic)

If a monster looked like it was shaking rapidly in place right after reaching
its spot: the movement code always drove at full speed straight at an exact
point, with nothing slowing it down as it got close. Right at the point, tiny
position noise from colliding with the target was enough to flip its direction
every physics step — and since that direction was always re-applied at full
speed, it showed up as a visible rapid back-and-forth shake instead of a clean
stop. Movement now eases off gradually within **Arrival Radius** (`0.3`
tiles) of wherever it's steering, down to a floor of **Arrival Min Speed
Factor** (`0.35`× normal speed — never a full stop, so give-way/separation can
still nudge it a meaningful amount right at its spot). Only applies while
calmly settling in; a genuinely stuck or yielding monster still gets full
force, since easing off there would undermine those fixes rather than help.

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

### A horde now breaks the ring open to make more room — fixed (automatic)

The scenario: you wall the King in so tightly that only one or two tiles around
it can actually be stood on, and one monster parks in the single opening. Before,
every other monster just queued behind that one — a whole horde stalled on one
attacker, forever, because there was only one open slot and it was taken.

Now each target exposes not only its **open** slots (tiles a monster can stand
on and hit from) but also its **walled** slots — tiles that *would* be valid
attack positions except a breakable wall is currently sitting on them. When a
monster arrives and finds every open slot taken, it claims the nearest walled
slot instead and routes to it; that routing comes back as "break this wall" on
that exact tile, so the monster breaks *that specific ring wall* open and takes
the fresh spot behind it. Because different overflow monsters claim different
ring tiles, a horde widens the breach around a boxed-in King instead of
single-filing through one gap — exactly the "break the surrounding walls to make
more slots" behavior.

Two guardrails keep this from turning into "monsters chew every wall in sight":

- **Only walls within attack range of the target ever count as walled slots** —
  i.e. the ring of walls immediately touching the King/tower, the actual seal.
  A maze wall out in the field is never a walled slot, so monsters still route
  *around* the maze as before; they only eat the seal ring right up against the
  thing they're attacking.
- **Only monsters already at the target claim them** (same close-range gate as
  normal slot claiming), so a monster still far away keeps walking the maze
  rather than smashing a shortcut.

So a small attack still just trickles in through a well-built maze; it's a
genuine *horde* against a *tightly sealed* target that now widens the breach.
With **Draw Attack Slots** on you can watch it happen: open slots draw as solid
cubes (green free / red taken), and walled would-be slots draw as **wire** cubes
— **amber** when free, **magenta** the moment a monster claims one and heads off
to break it open.

This also takes the edge off the last bit of monsters-blocking-each-other:
mutual jams around a sealed target were fundamentally a *too-few-slots* problem,
and more slots means less contention. On top of that there's now a proper
**shuffle chain**: a monster parked in its slot that gets bumped by any ally
still trying to get in will hop to the next free slot to let it past — and since
that hop briefly makes *it* the one still trying to get in, it bumps the next
monster along, so the shuffle propagates up a packed line until everyone has a
distinct spot. Crucially this keys off whether a monster has actually *reached
its own slot*, not just whether it's near the King — so a monster stuck a tile
short behind a wall of allies is correctly seen as "still needs in" and gets
made room for. And a monster that can't reach its exact slot isn't idle: as long
as it's within attack range it keeps hitting the King the whole time.

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
- **Give way at a target:** send a clump of monsters (say 4–6 zombies) at the
  King with only a narrow approach, so more arrive than can touch it at once.
  The first to reach it should slide aside along the King's edge as the next
  ones press in, until a small arc of them is attacking side by side — not a
  single zombie hogging the spot while the rest freeze behind it. A *lone*
  zombie attacking the King should just stand and attack, not fidget sideways.
- **Attack slots — ring vs. clump (the alcove test):** wall the King into a
  small pocket with one narrow opening, like the screenshots that started this,
  and send a crowd. The monsters that fit should settle onto distinct tiles
  around the opening and all attack. Tick **Draw Attack Slots** on `PathGrid` and
  watch in the Scene view — open slots draw as solid green/red squares, filling
  red as monsters claim them. Then widen the opening and confirm more slots
  appear. In the open field, the King should get a full ring of attackers instead
  of a blob on one face.
- **Horde breaks the ring open (the seal test):** wall the King in as tightly as
  you can — ideally leaving only one reachable tile around it — and send a *large*
  crowd. Rather than the whole horde stalling behind one attacker forever, the
  overflow monsters should start breaking the ring walls right around the King to
  open up more attack spots, and more of them should get in. With **Draw Attack
  Slots** on you'll see the walled would-be slots as **wire** cubes — **amber**
  free, turning **magenta** as monsters claim them to go break them open — then
  becoming ordinary green/red open slots once the wall falls. Confirm they only
  eat the walls right up against the King, not maze walls further out.
- **Funnel through a 1-wide gap:** build a wall line with a single 1-tile gap
  and send a bunched group at it. They should sort into single file and pour
  through — the ones behind briefly easing back so whoever's in front commits
  to the gap — rather than two wedging the opening and freezing. Watch that
  nobody sits stuck at the mouth for more than about a second.
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
- [ ] Draw Blocked Tiles / Draw Attack Slots actually show up (Play Mode + Scene view Gizmos toggle on, no need to select `PathGrid` anymore)
- [ ] Draw Targeting Debug shows lines to targets and a red flash on hits
- [ ] A monster settling into its spot eases to a stop instead of shaking
- [ ] A monster holding the only slot in a narrow opening steps aside for a blocked ally instead of permanently holding the doorway
- [ ] A monster assigned a far-side slot walks AROUND the King to it (when there's a lane) instead of pressing the near face
- [ ] A fully-walled King makes monsters break the wall nearest the King (the seal), walking the maze to it, not the nearest corridor wall
- [ ] With a maze that has a real path through it, monsters walk the path and never attack its walls
- [ ] A tightly-sealed King under a large horde gets its surrounding ring walls broken open for more attack slots (wire cubes in Draw Attack Slots), not one attacker with everyone queued forever
- [ ] Those ring-breaks only hit walls right against the King, never maze walls further out
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
