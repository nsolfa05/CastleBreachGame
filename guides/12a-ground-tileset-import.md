# Guide 12a — Placeholder art: the ground/grass tileset

**Goal:** swap the flat green placeholder square that currently fills the
whole map for a real (if temporary) grass sprite from a free tileset, with
zero visual-art-pipeline guesswork. This is part of **Guide 12 — Placeholder
Art Assets**, the first of two: this one is ground tiles, `12b` (later) will
be castle walls.

This guide assumes you've read `saving-and-committing.md` already — follow
that checklist at the end as always.

---

## Where this tileset came from

Two free options were considered:

- **Mana Seed "Seasonal Forest" spring sample** — turned out to be a thin
  *preview* of a paid bundle: one small sheet, only one grass/dirt
  combination, barely any variety. Not worth building on.
- **Gentle Forest** by Seliel the Shaper — genuinely free (the `v01`/`v02`/
  `v03` palettes are the free tier per its own readme), a full 16×16
  tileset with grass, dirt, flowers, cliffs/stone walls, water, and trees.
  Same artist as the paid Iconic Castle set, so if you ever want a wall
  placeholder that visually matches, this is a good source too (`12b`).

This guide uses **Gentle Forest v01 ("rabite forest" palette)**. Confirm on
[the itch.io page](https://seliel-the-shaper.itch.io/gentle-forest) that the
free-tier usage terms (free to use, credit appreciated) work for you — this
is explicitly a temporary placeholder you'll replace with your own art
later, so it doesn't need to be perfect, just clear to use now.

---

## What's already done for you (this commit)

Everything below was already committed to the repo, no manual asset-import
fiddling required:

- **`Assets/Sprites/Environment/GentleForestV01.png`** — the tilesheet
  itself, imported with correct pixel-art settings already set: Sprite Mode
  **Multiple**, Pixels Per Unit **16** (each source tile is 16×16px, and
  the project's convention is 1 tile = 1 Unity unit — see `GridMath.cs`),
  Filter Mode **Point** (no blur/anti-aliasing — keeps pixel art crisp),
  Compression **None**, Mesh Type **Full Rect** (avoids Tilemap edge-clipping
  issues with tightly-packed sprites).
- **One sprite pre-sliced out of that sheet**, `GentleForestV01_Grass01` — a
  plain, border-free, tileable patch of grass (column 1, row 6 on the sheet,
  0-indexed from the top-left — see the reference image below).
- **`Assets/Tiles/GroundTile.asset` already points at it.** The placeholder
  green tint is gone (`m_Color` reset to white — the real sprite carries its
  own color now). `CastleMapGenerator.cs` needed **zero code changes** —
  its header comment always said "swap for real art later, no code changes
  needed," and that held up.

So in principle, the moment you pull this and open Unity, the map should
already render with real grass instead of a flat green square.

---

## Step 1 — Pull and open

1. **Fetch → Pull** in GitHub Desktop (or `git pull`) before opening Unity,
   per the usual discipline.
2. Open the project. Let Unity finish importing — first import of a new
   texture can take a few seconds.
3. Check the **Console**: zero red errors.

---

## Step 2 — Verify the import (quick sanity check)

Click `Assets/Sprites/Environment/GentleForestV01.png` in the Project
window and look at the Inspector. You should see:

- **Texture Type:** Sprite (2D and UI)
- **Sprite Mode:** Multiple
- **Pixels Per Unit:** 16
- **Filter Mode:** Point (no filter)

If any of those look different, something about your Unity version's
default import behavior overrode the `.meta` — tell me and we'll sort it
out rather than guessing.

---

## Step 3 — See it in the game

1. Open the `Game` scene if it isn't already open.
2. Select the GameObject holding `CastleMapGenerator` in the Hierarchy.
3. Right-click the component header → **Generate Map** (or just press
   Play — it regenerates automatically on `Start()`).
4. The map should now be tiled with the grass sprite instead of the flat
   green placeholder.

If it still shows the old green square: select `Assets/Tiles/GroundTile.asset`
and check its **Sprite** field in the Inspector — it should show a small
grass thumbnail, not "None." If it shows "None," the pre-wired reference
didn't survive import for some reason — tell me and I'll help debug rather
than guess blind.

---

## Step 4 — Slice the rest of the sheet (do this now, saves a step later)

You'll want more than one grass tile eventually (variety, dirt, flowers,
walls in `12b`). Slicing the whole sheet now means it's ready whenever you
need it:

1. Select `GentleForestV01.png`, click **Sprite Editor** in the Inspector.
2. Top-left dropdown → **Slice**.
3. Type: **Grid By Cell Size**, Pixel Size: **X 16, Y 16**.
4. Click **Slice**, then **Apply** (top-right of the Sprite Editor window).

This should detect and preserve the existing `GentleForestV01_Grass01`
slice (it sits exactly on the 16×16 grid Unity generates) while adding
names for every other cell on the sheet. **After slicing, re-check Step 3**
— if `GroundTile.asset`'s Sprite field went blank, the slice didn't
preserve it the way expected; tell me and I'll fix the reference. I can't
verify this step myself since it runs entirely inside your local Editor.

---

## Picking other tiles from the sheet

Use this reference — it's the same sheet with a 16px grid and column/row
numbers overlaid:

![Gentle Forest v01 tile grid reference](images/gentle-forest-v01-grid.png)

To convert a (column, row) you like into the pixel rect Unity shows in the
Sprite Editor (top-left origin, row 0 at top): `x = column × 16`,
`y = row × 16`, width/height `16`. A few worth knowing:

- **Column 1, row 6** — the grass tile already in use.
- **Rows 0–2** — a dirt path autotile set (has blended grass edges, useful
  later for RuleTile work, not a plain single tile).
- **Columns 8–15, rows 0–3ish** — trees and bushes.
- **Columns 0–2, rows 9–14ish** — the gold-trimmed stone cliff/wall
  autotile ring — worth remembering for `12b` (castle walls), since it's
  from the same sheet and will match the grass tonally.

---

## Step 5 — Save and commit

Follow `saving-and-committing.md` exactly: File → Save, File → Save
Project, check GitHub Desktop's Changes tab matches what you did, commit,
push. Since this guide's assets were already committed on my end, your
local changes should just be whatever the Sprite Editor slicing produced
(an updated `.meta` file) plus nothing else — if GitHub Desktop shows more
than that changed, stop and tell me before committing.

---

## ✅ Checkpoint

- [ ] Pulled latest, Unity imports with zero Console errors
- [ ] `GentleForestV01.png` Inspector shows Sprite Mode Multiple, PPU 16,
      Filter Mode Point
- [ ] Map view shows real grass, not the flat green placeholder
- [ ] Sliced the full sheet via Sprite Editor, `GroundTile.asset` still
      shows a valid grass sprite afterward
- [ ] Committed and pushed

## Notes for later

- **RuleTile / autotiling isn't set up yet.** This guide deliberately kept
  ground as one plain repeating tile (per your call: simple swap first).
  `com.unity.2d.tilemap.extras` (RuleTile) is already installed
  (`8.0.3`, confirmed in `Packages/manifest.json`) — when you're ready for
  blended grass/dirt/cobblestone edges, that's the next step, likely
  alongside `12b`.
- **`12b` will cover castle walls**, probably reusing this same sheet's
  stone cliff/wall tiles (see "Picking other tiles" above) so the two
  placeholders read as one coherent (if temporary) art style.
- **This is explicitly temporary.** Nothing here blocks swapping in your
  own custom art later — `GroundTile.asset` just needs its Sprite field
  repointed, same as this guide did.
