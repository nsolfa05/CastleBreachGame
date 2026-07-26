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
   - **Blocking Layers** ← tick **Structure** and **King**. **Do NOT tick
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
   - **Box Collider 2D**
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

## Step 4 — Build the Gate prefab

1. In `Assets/Prefabs`, select **`Wall`**, **Ctrl+D** to duplicate, rename the
   copy **`Gate`**.
2. Double-click it (Prefab Mode) and change:
   - **Sprite Renderer → Color**: wooden brown (clearly different from Wall)
   - **Health → Max Health**: `25` (a door is flimsier than a wall)
   - **Barrier → Is Gate**: **check it**
3. Exit Prefab Mode.

That single checkbox is the whole Goblin rule: any monster whose definition has
**Passes Through Gates** ticked routes straight through a Gate as if it were
open ground. Your `Goblin` asset already has that ticked from Guide 08 — no
change needed there.

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
- **Nothing built = nothing changed:** with an empty field, monsters should
  behave exactly as they did before this guide — straight at their target, same
  crowding.

## Step 7 — Commit

`Phase 4: walls, gates and pathfinding`

Then **push**, and confirm on github.com that the new prefabs actually landed.

---

## ✅ Checkpoint

- [ ] PathGrid object exists, Wall Tilemap wired, Blocking Layers = Structure + King (not Enemy)
- [ ] Wall and Gate prefabs exist with Health, Destroy When Dead, and Barrier
- [ ] Gate has Is Gate checked; Wall does not
- [ ] Both registered in Build Options (hotkeys 5 and 6)
- [ ] Monsters route around walls instead of pressing into them
- [ ] Monsters never attack walls while any route exists
- [ ] Fully sealing the King makes them break through the seal
- [ ] Breaking a hole makes them immediately re-route through it
- [ ] Goblins walk through Gates; other monsters don't
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
