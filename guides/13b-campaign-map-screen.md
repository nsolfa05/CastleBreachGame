# Guide 13b — Campaign map screen

**Goal:** the Campaign button from `13a` now goes somewhere — a scrolling
world-map screen with 10 named level nodes zigzagging up and down along a
curved, dashed trail. Click-and-drag to scroll left/right, click a node to
jump into it (locked nodes do nothing and render greyed out). Second guide
of Phase 6.

This guide assumes you've read `saving-and-committing.md` already — follow
that checklist at the end as always.

---

## What's already done for you (this commit)

Five new scripts:

- **`Assets/Scripts/Systems/CampaignProgress.cs`** — persisted "how many
  nodes are unlocked" count (defaults to 3, so you can see both locked and
  unlocked nodes without needing a real win condition yet). Two Editor menu
  items for testing: **Tools → Castle Breach → Campaign - Unlock Next
  Level** / **Reset Progress**.
- **`Assets/Scripts/Campaign/CampaignNode.cs`** — one level node: name
  label, locked/unlocked color, and `Activate()` (loads the `Game` scene if
  unlocked, does nothing if locked). Every node currently loads the same
  `Game` scene regardless of which one — there's no real per-level data
  yet, that's `14b`. Nothing here needs to change when that lands; only
  what `Activate()` loads does.
- **`Assets/Scripts/Campaign/CampaignCameraAndInput.cs`** — click-and-drag
  horizontal camera panning, clamped to a min/max X you'll set once nodes
  are placed. Also detects a genuine click (as opposed to a drag) and
  activates whatever node is under the cursor when you release. Camera Y
  never moves — this only scrolls left/right, per your call, even though
  nodes zigzag vertically.
- **`Assets/Scripts/Campaign/CampaignTrail.cs`** — the curved dashed line
  connecting nodes in order. Built from a Catmull-Rom spline (Unity has no
  built-in spline component) through an ordered list of node Transforms,
  rendered with a `LineRenderer` and a small procedurally-generated dash
  texture — no image asset needed. Marked `[ExecuteAlways]`, so dragging a
  node around in the Scene view redraws the trail immediately, live, even
  outside Play mode — that's what makes hand-arranging the zigzag layout
  practical.
- **`Assets/Scripts/UI/BackButton.cs`** — small reusable "load this one
  scene" handler for simple Back buttons that don't need a whole dedicated
  menu script (`TitleMenu`/`SettingsMenu` stay as they are — each owns more
  than just one button).

**This is a World Space scene, not a UI Canvas**, unlike Title/Settings —
deliberately, since Unity's UI system has no native spline/dashed-line
support, and this also sets up cleanly for dropping in real background art
later (a world-space sprite behind everything, camera panning across it) —
no rework needed when that happens.

**"Moveable" here means Editor-only, by hand** — drag a node GameObject
around in the Scene view like any normal object, same as decorating the
tree view or arranging anything else. That's different from an in-game
drag tool for players or you-while-playtesting; that's `15c`'s job later,
part of the actual Map Builder. This gets you a good, easily-adjusted
layout now without duplicating that future work.

---

## Step 1 — Pull and verify

1. **Fetch → Pull**, let Unity recompile, check the **Console** for zero
   errors. Confirm the two new Tools menu items exist
   (**Tools → Castle Breach → Campaign - ...**).

---

## Step 2 — Build the Campaign scene and camera

1. **File → New Scene** → **File → Save As** → `Assets/Scenes/Campaign.unity`.
2. Select **Main Camera**. Confirm **Projection** is **Orthographic**
   (should already default to this in a 2D project). Set **Size** to
   something like `8` — you'll retune this once nodes are placed and you
   can see how much vertical zigzag room you actually need.
3. **Add Component → Campaign Camera And Input** on Main Camera. Leave
   **Min X**/**Max X** at their defaults for now — you'll set these for
   real in Step 5, once nodes exist to measure against.

---

## Step 3 — Build one node, then duplicate it 9 times

Build the first node completely before duplicating — much less rework than
duplicating an incomplete one ten times.

1. Hierarchy → **Create Empty**, name it **`Node 1`**.
2. **Add Component → Sprite Renderer**. Set its **Sprite** to the existing
   `Circle` sprite (`Assets/Sprites`). Size it up a bit via the object's
   **Scale** (e.g. `1.5, 1.5, 1`) so it reads clearly at this camera zoom.
3. **Add Component → Circle Collider 2D** — this is what lets
   `CampaignCameraAndInput` detect a click on this node. Default radius is
   fine.
4. Right-click `Node 1` → **3D Object → Text - TextMeshPro** (the
   world-space variant, not the UI one — no Canvas needed for it). Name it
   **`Label`**, position it just below the sprite (e.g. local position
   `0, -1, 0`). Set a reasonable font size and center alignment; text
   content doesn't matter here, the script overwrites it.
5. **Add Component → Campaign Node** on `Node 1` itself (not the label).
   Fill in:
   - **Index**: `1`
   - **Level Name**: `Level 1` (or a real placeholder name you like)
   - **Icon**: drag `Node 1`'s own Sprite Renderer in
   - **Label**: drag the `Label` child's TextMeshPro component in
   - **Unlocked Color** / **Locked Color**: defaults (white / grey) are
     fine to start
6. Select `Node 1` in the Hierarchy, **Ctrl+D** nine times. Rename each
   copy `Node 2` through `Node 10`, and on each one's **Campaign Node**
   component update **Index** (`2`..`10`) and **Level Name** to match.

---

## Step 4 — Arrange the zigzag

Drag each node (in the Scene view, using the Move tool) into a left-to-right
line with varying height — node 1 leftmost, node 10 rightmost, heights
zigzagging up and down between them however looks good to you. There's no
"correct" layout here, it's your call — this is exactly the kind of thing
that's much easier to eyeball live than to have described in coordinates.

Rough starting spread to work from: space nodes roughly 4–5 world units
apart horizontally (so 10 nodes span ~40–45 units total), zigzag height
maybe ±2 units. Adjust freely.

---

## Step 5 — Wire up the trail and camera bounds

1. Hierarchy → **Create Empty**, name it **`Trail`**.
2. **Add Component → Line Renderer**. On it: set **Width** to something
   thin like `0.1` (both start and end width). **Confirm Use World Space
   is CHECKED** (Unity's default — it should already be on, just verify).
   This matters: node positions are real world-space coordinates, and if
   this ends up unchecked, the line gets drawn as if those numbers were
   local offsets from `Trail`'s own transform instead — putting it
   somewhere completely different from your nodes, which looks exactly
   like "no line is showing up at all."
3. **Add Component → Campaign Trail** (also on `Trail`). Expand **Nodes In
   Order** and drag in `Node 1` through `Node 10`, **in that exact
   left-to-right order** — the spline is drawn through them in list order,
   not by scanning positions.
4. You should immediately see a curved dashed line connecting all 10 nodes
   in the Scene view, live-updating as you drag any node around (try it).
   To tune how it looks, all of these update live as you change them in
   the Inspector (no need to re-enter Play mode):
   - **Line width** — the `Line Renderer` component's own **Width** field
     (not on `Campaign Trail`).
   - **Dash Length** / **Gap Length** (on `Campaign Trail`) — each is a
     real world-space size. Bigger **Gap Length** relative to **Dash
     Length** = fewer, more spaced-out dashes; equal values = the classic
     even dash-dash-dash look; a big **Dash Length** with a small **Gap
     Length** starts to look like a solid line with periodic breaks.
5. Back on **Main Camera**'s **Campaign Camera And Input** component: set
   **Min X** to roughly `Node 1`'s X position minus a couple units, **Max
   X** to roughly `Node 10`'s X position plus a couple units — enough
   margin that the outermost nodes aren't jammed against the screen edge
   at either scroll extreme.

---

## Step 6 — Back button and cursor

1. Hierarchy → **UI → Canvas** (Screen Space - Overlay renders on top of
   the world-space trail regardless of camera position, so this works
   fine layered over everything above). Set its Canvas Scaler the same way
   as `13a` (Scale With Screen Size, 1920x1080, Match Width Or Height, 0.5).
2. Add a **Back** Button (`UI → Button - TextMeshPro`) — position it in a
   corner, e.g. top-left, so it's never near where you're trying to click a
   node. Rename it `Back`, change its child TMP text to say "Back".
3. On the **`Back`** Button GameObject itself: **Add Component → Back
   Button** (new — a small reusable "load this one scene" script, for
   simple cases like this that don't need a whole dedicated menu script).
   Leave **Scene Name** at its default (`Title`).
4. Wire its **On Click ()**: `+`, drag the **`Back`** GameObject itself
   into the object slot, pick `BackButton.OnPressed`.
5. Drag the `CursorCanvas` prefab (`Assets/Prefabs`, from `13a`) into this
   scene too.

---

## Step 7 — Register the scene and test

1. **File → Build Settings** → add `Campaign` (alongside `Title`,
   `Settings`, `Game` from `13a`).
2. Play from `Title` → **Campaign**. You should see:
   - The zigzagging trail with all 10 nodes, curved and dashed.
   - Nodes 1–3 in their unlocked color, 4–10 greyed out (locked).
   - **Click-and-drag** anywhere empty to pan left/right, clamped at both
     ends.
   - **Click** (no drag) an unlocked node → loads `Game`.
   - **Click** a locked node → nothing happens.
   - Use **Tools → Castle Breach → Campaign - Unlock Next Level** (while
     NOT in Play mode — PlayerPrefs writes from Edit mode persist same as
     any other) a few times, replay, confirm more nodes light up.
   - Custom cursor visible and working, same as `13a`.

---

## Step 8 — Save and commit

Follow `saving-and-committing.md`: File → Save, File → Save Project, check
GitHub Desktop's Changes tab (expect the four new scripts + `.meta`s, the
new `Campaign.unity` scene + `.meta`, and the Build Settings change),
commit, push.

---

## ✅ Checkpoint

- [ ] Pulled latest, zero Console errors, both Campaign Tools menu items
      present
- [ ] 10 nodes built, named, indexed 1–10, arranged in a left-to-right
      zigzag
- [ ] Trail connects all 10 in order, curved and dashed, updates live when
      you drag a node in the Scene view
- [ ] Camera Min X/Max X set so scrolling clamps sensibly at both ends
- [ ] Click-and-drag pans; a genuine click (no drag) on an unlocked node
      loads `Game`; a locked node does nothing and renders greyed out
- [ ] Back button wired via `BackButton`, returns to `Title`
- [ ] Custom cursor present and working
- [ ] `Campaign` added to Build Settings
- [ ] Committed and pushed

## Notes for later

- **`13c`** wires the win screen to actually call `CampaignProgress.
  UnlockNext()` on victory, plus gives win/lose screens a real "back to
  menu" button — right now progression only advances via the debug Tools
  menu items.
- **`14b`** (Phase 7) replaces every node's identical "load `Game`" wiring
  with real per-level data — grid layout, waves, King stats, the works —
  once that exists, each node loads its own actual level instead of the
  same vertical slice.
- **Locked-node visual is just a color tint for now** (placeholder policy,
  same as everything else) — a lock icon overlay or similar can swap in
  whenever real art arrives, no code changes needed beyond assigning a
  sprite.
