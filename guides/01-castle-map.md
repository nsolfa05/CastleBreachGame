# Guide 01 — Build the castle map

**Goal:** The 40×30 grid from the design doc (§3.1–3.3) renders in the scene: green
ground everywhere, gray 2-tile-thick walls on all four sides, and four brown
half-transparent gates. Walls physically block movement; gates don't.

You'll learn: sprites, Tile assets, Tilemaps, adding components, and running a
script command from the Inspector.

---

## Step 1 — Set up the scene

1. The template opens with a scene called `SampleScene`. We'll make it the game
   scene: **File → Save As…**, navigate to the `Assets/Scenes` folder, name it
   **`Game`**, save.
2. In the Project panel you can right-click the old `SampleScene` asset → Delete
   (optional cleanup).

## Step 2 — Create the placeholder sprites

Unity can generate simple white shapes — perfect for the doc's placeholder policy.

1. In the Project panel, right-click `Assets` → **Create → Folder**, name it `Sprites`.
2. Open the `Sprites` folder. Right-click inside it → **Create → 2D → Sprites →
   Square**. Name it `Square`.
3. Same again: **Create → 2D → Sprites → Circle**. Name it `Circle`.

These are plain white shapes; scripts and tile settings tint them whatever color is
needed, so one square serves as ground, wall, gate, player, tower, etc. until real
art exists.

## Step 3 — Create the Tile assets

A **Tile** is a paintable asset used by Tilemaps (Unity's grid system).

> **Newer Unity versions (6.5+) removed the plain "Tile" entry** from
> **Create → 2D → Tiles** — that menu now only offers **Animated Tile** and
> **Auto Tile** (a submenu of Rule Tile variants for auto-blending terrain
> edges), neither of which is what we want. Instead, generate a basic Tile
> asset via the **Tile Palette**, which creates one automatically the moment
> you drag a sprite into it:

1. Create another folder: `Assets/Tiles`.
2. **Window → 2D → Tile Palette.** Click the palette dropdown (top-left of
   that window) → **Create New Palette**. Name it anything, e.g.
   `ScratchPalette` — it's just scaffolding to generate the asset below, your
   map won't use it.
3. Drag the **`Square`** sprite (from `Assets/Sprites`) into the Tile
   Palette's checkered grid area. A save dialog appears — save it into
   `Assets/Tiles`. Unity creates a real Tile asset there (named after the
   sprite) and paints it into the scratch palette.
4. Find that new Tile asset in `Assets/Tiles`, select it, and rename it
   (**F2**) to `GroundTile`.
5. Duplicate it twice (select it, **Ctrl+D**): name the copies `WallTile` and
   `GateTile`. They can all share the same white square sprite — the map
   generator colors them per-cell in code, not on the asset itself.
6. You can delete the `ScratchPalette` asset now if you like — it was only
   needed to create the three Tile assets above.

<details>
<summary>If your Unity version still shows a plain "Tile" option</summary>

Right-click inside `Assets/Tiles` → **Create → 2D → Tile** (or
**Create → 2D → Tiles → Tile**), name it `GroundTile`, then set its
**Sprite** field to `Square` in the Inspector (drag the sprite onto the
field, or use the ⊙ picker). Duplicate twice as in steps 4–5 above.
</details>

## Step 4 — Create the Grid and three Tilemaps

1. In the **Hierarchy**, right-click empty space → **2D Object → Tilemap →
   Rectangular**. This creates a `Grid` object with a child `Tilemap`.
2. Rename the child `Tilemap` to **`Ground`** (select it, press F2).
3. Right-click the `Grid` object → **2D Object → Tilemap → Rectangular** again —
   this adds a second tilemap under the same Grid. Rename it **`Walls`**.
4. Once more: add a third tilemap, rename it **`Gates`**.
5. Set draw order so walls appear above ground: select `Ground`, and in its
   **Tilemap Renderer** component set **Order in Layer = 0**. `Gates` → **1**.
   `Walls` → **2**.
6. Make walls solid: select `Walls`, click **Add Component** (bottom of Inspector),
   search for and add **Tilemap Collider 2D**. That's it — every wall tile is now
   physically solid; the `Gates` and `Ground` tilemaps get no collider, so they
   stay walkable.

## Step 5 — Add the map generator

1. Hierarchy → right-click → **Create Empty**, name it **`MapGenerator`**.
2. With it selected: **Add Component** → type `Castle Map Generator` → add it.
3. Fill in its fields (drag from the Hierarchy / Project panel):
   - **Ground Tilemap** ← the `Ground` object from the Hierarchy
   - **Wall Tilemap** ← `Walls`
   - **Gate Tilemap** ← `Gates`
   - **Ground Tile / Wall Tile / Gate Tile** ← the three Tile assets from `Assets/Tiles`
4. Notice the **Wall Regions** and **Gate Regions** lists are pre-filled with the
   exact coordinates from the design doc (`A1–B30`, `B14–B17`, …) — expand them and
   compare with §3.2/3.3. This is the doc's "editable in-Editor" rule at work:
   future maps are just different entries in these lists.
5. Now run it: **right-click the "Castle Map Generator" component header** (the bold
   title text in the Inspector) and choose **Generate Map** from the menu.

You should see the whole castle appear in the Scene view: green field, gray border
walls, and four brown gaps. If the Scene view is looking at the wrong spot, press
**F** with the Grid selected to frame it.

Re-running **Generate Map** is always safe — it clears and rebuilds. If you tweak a
color or region, just generate again.

## Step 6 — Point the camera at the map

1. Select **Main Camera** in the Hierarchy.
2. In the Inspector set **Position** to `X 20, Y 15, Z -10` (map center; the camera
   must stay at negative Z in 2D).
3. In the **Camera** component set **Size** (orthographic size) to `16`. The Game
   view tab should now show the whole castle.

## Step 7 — Save and commit

1. **Ctrl+S** to save the scene. (The tilemap contents save with the scene.)
2. Commit & push, e.g.: `Castle map: grid, walls and gates from design doc coordinates`

---

## ✅ Checkpoint

- [ ] Game view shows the full 40×30 castle: green ground, gray walls, 4 brown gates
- [ ] The wall thickness is 2 tiles; gates only pierce the inner tile (compare §3.3)
- [ ] Selecting `MapGenerator` shows the doc's coordinates in the region lists
- [ ] Scene saved, work committed

**Next:** [Guide 02 — Player & camera](02-player-and-camera.md)
