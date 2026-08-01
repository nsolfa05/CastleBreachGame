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

### Two crowd-polish fixes: no more slot jitter, no more head-on lock (automatic)

Two smaller issues that showed up once the ring was filling properly:

- **Jitter on a slot.** A monster sitting on its slot kept driving at the
  arrival-speed floor straight at the exact tile centre, so tiny collisions
  flicked it back and forth across the spot. Now a monster that has actually
  reached its own slot and has nothing else to do simply **holds position** —
  only crowd separation still nudges it, and gently, so it stops dead on its
  slot instead of buzzing. (It leaves the instant it genuinely needs to: it's
  handed a new far slot by the shuffle, or gets shoved clear off the tile.)
- **Two monsters shoving each other head-on.** In a 1-wide ring there's no
  sideways room to slip past, so two monsters meeting head-on both read as
  stuck and both lean in, holding the jam forever. That's now broken
  deterministically: when two jammed monsters are pressed together, the
  lower-priority one (an arbitrary but stable tiebreak) **eases back** to let
  the other through, then follows once it's clear. In a bigger pile exactly one
  monster keeps moving at a time and the jam drains instead of locking.

A key part of both: "am I done moving?" now means **"am I on my own slot?"**, not
the old "am I near the King?" — a monster wedged a tile short of its slot used to
count as home (so it never tried to un-stick and never made room), and that one
conflation was behind most of the leftover pushing.

### A real bug: monsters had gravity on — fixed (automatic)

While chasing the last bit of jitter, a genuine bug turned up: this is a
top-down game, but the project's global 2D gravity was still Unity's default
`(0, -9.81)`, and the Monster prefab's Rigidbody 2D still had **Gravity Scale
1** — so every monster was quietly being pulled downward, every physics step,
the whole time. The movement code sets velocity fresh each step, so it mostly
papered over this, but not entirely: the pull was reapplied faster than it
could ever fully cancel out, which is exactly the kind of constant tiny
correction that reads as jitter — worst on a monster meant to be holding still
on its slot, and a steady background wobble on every other monster's path
(feeding the "bounces off allies" issue below too). Fixed in code — Awake now
forces Gravity Scale to `0` on every monster, so the prefab's own value can't
reintroduce it — plus a little Linear Damping (`2`) so any velocity left over
from a collision actually dies out instead of carrying into the next step as
an overshoot. Nothing to change on the prefab; if you ever inspect it and see
Gravity Scale still at `1`, that's fine, code overrides it on spawn.

### Monsters bouncing off each other instead of sliding past — fixed (automatic)

Reported alongside the jitter: monsters crossing paths (e.g. two overflow
monsters heading to break their own separate ring walls) would visibly bounce
off each other two or three times before finally sliding past, instead of
gliding around each other smoothly. The gravity bug above was part of it, but
there was a second, structural cause: the only response to an oncoming ally
used to be *reactive* — physically collide, then wait for stuck-recovery to
measure "no progress" over almost a second before it started pushing sideways.
Those few checks show up as visible bounces.

Fixed with a new **proactive** term (`HeadOnAvoidance`, weighted by **Head-On
Avoid Strength**, default `0.5`): a monster now watches for an ally actually
*moving toward it* (not just standing nearby, which plain separation already
handles) and starts easing sideways before they ever actually meet — the more
directly their paths oppose, the stronger the nudge. Two monsters on a
collision course now peel apart and slide past each other early, instead of
colliding first and correcting after the fact.

### Migration reaching clear across the ring — fixed (automatic)

Still more pushing turned up even after the above: two monsters that had both
already *arrived*, sitting right next to each other, would still sometimes
shove past one another for no visible reason. Cause: when a settled monster
migrates to make room for a blocked ally (see the alcove/doorway fix earlier),
it used to search for its new slot the same way the very first claim does —
nearest by straight-line distance, unbounded. That's fine for the first claim,
made from a distance before the crowd has formed. But a migration happens
*inside* an already-packed ring, and "nearest, anywhere" can easily land on a
tile clear on the far side — sometimes the very tile another monster is
currently standing on (or walking away from) — so the two of them end up
crossing paths and shoving through each other just to swap places.

Migration now PREFERS a tile within **Migrate Search Radius** (`1.6` tiles —
just past one tile over, including diagonally) of the monster's current
position — a genuine local shuffle, "step to the free tile right next to me,"
instead of a walk across the ring. The ordinary first-time claim is untouched
and still searches freely, which is what correctly spreads an approaching
crowd into a full ring in the first place.

**Correction, found right after the above shipped:** bounding this
unconditionally broke the doorway/chokepoint case from much earlier — a
monster holding the single tile in a 1-wide wall gap has NO nearby alternative
by definition (wall on both sides), so a hard bound just meant it could never
migrate at all and permanently blocked the doorway again, with the ally behind
it stuck endlessly backing up and bumping into it. Migration now tries nearby
first, and only when NOTHING is free within that radius falls back to
searching the whole ring — so a packed-but-roomy ring still only ever shuffles
locally, while a genuine single chokepoint can still clear itself the way it
always could. Only when there's truly no free slot anywhere does a monster
keep its current one and stay put. (A target that's over capacity in every
direction gets more room the other way — overflow monsters break open a walled
slot instead of forcing an existing holder to relocate; see "A horde now
breaks the ring open" above.)

### A getting-stuck-then-reclaiming gap — fixed (automatic)

One more crossing-paths case turned up, this time between two monsters that
were BOTH still mid-approach (neither had arrived yet) — not covered by the
migration fix above, since migration only ever applies to an already-arrived
holder. When a monster gets physically stuck reaching its claimed slot, it
drops the claim and immediately tries for a new one — but that reclaim used to
run the exact same unbounded nearest-search as a first-time claim. Two
monsters jammed against each other could each release, then each re-grab a
tile still on the wrong side of the other, ping-ponging between the same
couple of crossed assignments.

Fixed the same way as migration: for a short window after a stuck-triggered
release (**Stuck Reclaim Local Window**, `1.5` seconds), the reclaim only
considers tiles within Migrate Search Radius of the monster's current
position — a genuine local pick instead of a ring-wide search. A monster that
never held a slot at all still searches freely, same as before.

### "Claim where you stand" — the big stabilizer (automatic)

All the fixes above chip at *symptoms* of the same root problem: a monster
reserves a slot from a distance, then has to physically walk to that exact tile
— and if the crowd has shifted by the time it arrives, reaching its reserved
tile means shoving back through everyone. That's the crossing/pushing, and it's
also why a crowd would look like it was about to settle and then re-jumble: the
assignment kept churning because everyone was committed to a *specific* tile
rather than to *a* good spot.

New rule (this is the one you suggested): **if a monster is physically standing
on a valid, free slot for its target, it just claims THAT one and is done** —
dropping whatever tile it had reserved earlier. It adopts wherever it has
already drifted instead of crossing back for its reservation. Because claiming
the tile you're already on requires no travel, it removes most of the
pushing outright: a monster almost never has to walk *through* other monsters
to reach its slot anymore; it settles onto the first good tile it wanders onto
and stops there.

This also quiets the shuffle-chain churn as a side effect. A monster that has
opportunistically settled reads as *placed*, so it no longer registers as an
"ally still trying to get in" bumping the monster ahead of it — which means
holders stop being asked to migrate for allies that have, in fact, already
found a spot. Migration now mostly only fires for the genuine chokepoint case
it's actually meant for (someone boxed in behind a doorway with nowhere of
their own to stand). The explicit reserve-a-distant-slot path still exists and
still spreads an incoming crowd into a ring; "claim where you stand" just wins
whenever a monster is already on something valid.

### Two principled upgrades: reciprocal avoidance + migration hysteresis (automatic)

After chasing the crowd behavior patch by patch for a while, two ideas from how
real crowd-heavy games solve this got pulled in properly, replacing/backing up
some of the ad-hoc pieces:

- **Reciprocal collision avoidance (the RVO/ORCA idea).** The old head-on nudge
  only looked at whether a neighbor was *facing* toward this monster. The
  replacement (`AvoidNeighbors`, weighted by **Avoid Strength**, default `0.6`)
  asks the proper question: from our two *velocities*, are we actually
  converging? When two monsters genuinely are, they BOTH veer to the same
  rotational side (each to its own right relative to the line between them —
  which, because that line points opposite ways for the two of them, sends them
  to opposite world sides). They split the dodge and glide past instead of one
  bouncing off the other. This is the "reciprocity" real crowd solvers rely on:
  neither has to win, both give a little, and it resolves cleanly. It's distinct
  from separation, which only reacts to how *close* bodies are, not where
  they're headed. (This is a cheap per-neighbor form of the idea, not a full
  ORCA solver — enough for this game's scale.)

- **Migration hysteresis (the stable-assignment idea).** A monster no longer
  gives up its slot the instant an ally brushes past it. The ally must have been
  *continuously* blocked behind it for **Migrate Blocked Dwell** (default `0.5`
  s) first. In a packed ring monsters are always momentarily jostling past each
  other; migrating on every transient bump was a big part of the "almost
  settles, then re-jumbles" churn. A real chokepoint block (a doorway) is
  *persistent*, so it clears the dwell and still triggers a migration — the
  case migration is actually meant for — while the fleeting bumps that caused
  the churn now get ignored.

Both are plain tunables on the Monster prefab (Avoid Strength / Migrate Blocked
Dwell); set either to a low value or `0` to dial it back toward the old
behavior if a value ever feels off.

**On corridors / far slots going unfilled (the recurring one):** the failure was
a crowd bunching at the *mouth* of a walled King while free slots on the far side
of the ring sat empty — nobody was assigned to walk around to them. Rather than
compute a global "depth" (fragile on maps that have their own border walls, since
there's no clean "outside" to measure from), this is now handled **locally**:
when a settled monster steps aside for a blocked ally, it steps to the nearby
free tile **furthest from the crowd pressing in on it** — i.e. one tile *deeper*,
away from where the newcomers are coming from — instead of just the nearest tile.

That single directional bias makes the shuffle **propagate toward the back**:
the front monster steps deeper, the newcomer takes its old spot, that newcomer is
now the one being pressed and steps deeper in turn, and so on — the ring fills
from the far side inward without anyone needing to know where "the back" is. It's
still a one-tile-at-a-time local move (same `Migrate Search Radius` bound), so it
never turns into a walk across the ring, and it falls back to a whole-ring search
only for the genuine 1-wide-doorway case. In an open field (no enclosure, crowd
roughly all around) "away from the crowd centroid" just spreads monsters apart,
which is the same thing nearest-slot did — so it doesn't hurt the open case.

This is the practical version of the "fill from the back" idea. A full global
slot *assignment* (angular matching around the target) is still the theoretical
best and remains the noted next step if this local version isn't enough — but it
subsumes the corridor case into normal migration instead of needing a separate
mode.

### Pressure gradient — "flood like water & surround" (automatic, tunable)

The first step of a deliberate shift toward *emergent* crowd positioning (letting
attack positions arise from crowd behavior instead of a rigid slot assignment).
This piece helps regardless of which system you use, and it's the dial you asked
for: **how hard the rear ranks push the front.**

Every monster now measures how boxed-in it is behind allies heading to the *same*
target (`ForwardCrowdPressure`) and throttles its own forward speed by it
(**Rear Push Falloff**, default `2`, with a **Rear Push Floor**, default `0.2`, so
a buried monster still creeps). A front-rank monster has nothing ahead → full
speed → it holds the line and attacks. Ranks behind slow progressively, so they
*don't* compress the front through the structure; instead their slow drive gets
turned sideways by separation, and they **spill around and surround** the target
(and fill a corridor like water) rather than piling on its near face. As
front-liners die or move, the pressure behind drops and the next rank flows into
the opening on its own.

Tuning: **Rear Push Falloff** is the main knob — turn it *up* for more water-like
flow and less shoving, *down* (or `0`) for the old ram-forward behavior. This is
also the groundwork for optionally turning the slot system *off* entirely
(`Use Attack Slots` = false on the Monster prefab) and letting positions emerge
purely from crowd flow — the two can be A/B'd in the Inspector on the same scene.

### Real bug found testing slots off: a monster could freeze indefinitely at a doorway — fixed

With `Use Attack Slots` off, a monster could get stuck at the mouth of a walled
target for a very long time (minutes, not seconds) — no oscillation, just a hard
freeze. Root cause: without slots, "arrived" collapses to `settledAtTarget`, a
plain straight-line distance check with no notion of whether the monster is
actually well-positioned — just close enough by geometry. A monster sitting right
at (or just inside) a doorway can satisfy that easily. The problem: **being
"arrived" permanently disables stuck-recovery** (it resets to zero every single
frame arrived reads true), and the only other responder for "someone needs my
spot" — `GiveWayVelocity` — only slides a monster **tangentially** along the
target's surface, which at a narrow gap just presses it into the flanking wall
instead of helping it advance. Once a monster fell into this state at a
chokepoint, nothing was left that could ever force further progress.

Fixed with **Rear Pressure** (`RearCrowdPressure`, mirroring the pressure
gradient's forward-facing measurement but for allies pressing in from BEHIND,
still trying to reach the same target). Above **Rear Pressure Threshold**
(default `0.4`), a slot-less monster:
1. **Stays eligible for stuck-recovery** even while nominally "arrived" — this
   is the actual fix for the freeze; a monster under real rear pressure is
   clearly not a comfortable, uncontested settle, so it keeps escalating
   sideways until it actually breaks free.
2. **Stops yielding to a leader ahead of it** — backing off with a crowd
   pressing in from behind just rams them, so a pressured monster holds or
   pushes forward instead of retreating.
3. **Skips arrival easing** and drives at full strength instead of gently
   coasting in — easing off is for a monster that's genuinely done, not one a
   crowd is still counting on to make room.

This is also the practical form of "crowd pressure makes you commit" — a
monster is never literally shoved by its neighbors (still a deliberate
non-goal, see below), it just personally becomes less willing to wait or ease
off the more of a crowd is counting on it to move. `Rear Pressure Threshold =
0` turns the whole feature off (old behavior).

**Correction, found retesting the exact doorway that started this:** the first
version of `RearCrowdPressure` excluded any neighbor whose OWN `IsPlaced()`
already read true — but with slots off, that's just `settledAtTarget` again,
the exact loose signal this feature exists to route around. An entire cluster
jammed at a doorway can ALL satisfy it simultaneously (everyone's within
straight-line attack range of the King even though nobody's actually gotten
anywhere), which made every monster read every other monster as "already
placed" and exclude it — so pressure came out ~0 for the whole group, in
precisely the scenario meant to detect it. The freeze persisted (25s in
testing, vs. the original 45s+ — the fix wasn't doing nothing, it just wasn't
reliably engaging).

Fixed by dropping that filter and using tight **proximity** instead: only a
same-objective neighbor within `Separation Radius` (not the wider `Give Way
Radius`) counts, regardless of whether it also happens to read as arrived. A
comfortably-settled, non-jammed group spaces itself out beyond separation's own
push radius once forces balance; two same-target bodies staying persistently
this close only happens when there's genuinely nowhere for separation to push
them apart to — a real jam, and a signal a loose distance check can't fake.

**On literally pushing monsters sideways to help them squeeze through** (a
question that came up): deliberately not done. Every force in this system is
self-authored — a monster only ever computes its OWN velocity by reading
neighbors, never applies force TO one. This project already tried the opposite
early on (one monster physically sliding another aside) and reverted it for
being unpredictable — letting monster A's motion depend on a force B applies to
it creates real feedback chains (A pushes B, B's collision response pushes C,
which can bounce back and disturb A) that are much harder to reason about than
what's here now, and can look like ragdolling rather than intentional movement.
Rear Pressure gets the "crowd squeezes through" feel without that risk.

### Big-picture note: the slot system at scale

Worth recording for later, since it came up while chasing these crowd bugs:
the *slot concept* (discrete claimed standing tiles) is the right approach for
a horde converging on one or two structures, and is what most tower-defense/
RTS games with big crowds actually do — it's not something to redesign away.
The part that scales badly is routing: right now each monster solves its own
path with a rate-limited BFS. A **flow field** (one shared vector field per
target, computed once when the grid changes, giving every monster an O(1)
per-frame lookup instead of its own search) is the standard fix for "many
units converging on one target" and would be the right next step if monster
counts ever become a real bottleneck. Not pursued yet — see the shared
neighbor-query change below for the cheap win that WAS done this round.

### Cheap performance win: one neighbor scan per monster per frame (automatic)

Every crowd behavior above — separation, yield-to-leader, give-way,
ally-queued-behind, stand-down-for-stuck-neighbor, head-on avoidance — used to
run its own `Physics2D.OverlapCircle` every physics step, up to six broad-phase
queries per monster per frame, each scanning the same small patch of space for
overlapping results. With a couple hundred monsters on screen that's a lot of
redundant physics work for identical answers.

Now there's exactly **one** query per monster per frame (`RefreshNeighbors`,
called once at the top of `FixedUpdate`, sized to whichever individual crowd
radius reaches farthest). Every behavior above reads from that one shared
result and applies its own radius as a plain distance check instead of
querying again. Purely an internal performance change — none of the crowd
behaviors themselves changed, so nothing above should look or feel any
different, just cost less CPU per monster.

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
- [ ] Monsters sitting on their slots hold still and attack instead of jittering/shoving each other when there's room for everyone
- [ ] Two monsters meeting head-on in a 1-wide ring resolve (one eases back, the other passes) instead of locking against each other
- [ ] Monsters already parked on a slot don't walk clear across the ring / cross paths through each other when a nearby ally is bumping them — they only ever shuffle to the tile right next to them, or stay put if none is free
- [ ] Two still-approaching monsters that get jammed against each other don't ping-pong between crossed slot assignments after getting unstuck
- [ ] A monster holding the single tile in a 1-wide wall gap still eventually migrates away to make room, instead of permanently blocking the doorway while the ally behind it just backs up and bumps forward forever
- [ ] Monsters settle onto whatever valid slot they drift onto and STAY (crossed targeting lines / a crowd that "almost settles then re-jumbles" should be largely gone) rather than shoving back across the pack to reach a tile they reserved from a distance
- [ ] Two monsters on crossing/oncoming paths curve past each other (both giving a little) instead of colliding and bouncing — reciprocal avoidance
- [ ] A settled monster only steps aside for an ally that stays blocked behind it (a real doorway jam), not for one that just brushes past in the crowd — migration hysteresis
- [ ] A crowd bunched at the mouth of a walled King progressively fills the FAR slots too (holders step deeper as newcomers press in) instead of leaving the back of the ring empty
- [ ] With Rear Push Falloff up, a crowd floods a corridor and flows AROUND to surround a structure (front holds, rear spills sideways) instead of the rear compressing the front into the near face
- [ ] With slots off, a monster wedged at a doorway with allies pressing behind it no longer freezes indefinitely — it keeps working the jam (stuck-recovery escalation) until it actually breaks free
- [ ] Monsters no longer settle into a faint vertical wobble while holding a slot (the gravity fix)
- [ ] Two monsters crossing paths (e.g. heading to break separate ring walls) slide past each other instead of visibly bouncing off each other first
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
