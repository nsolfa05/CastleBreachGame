# Guide 12d — Camera pixel snapping (fixing tile-edge shimmer)

**Goal:** stop the shimmering hairline that appears between tiles while the
camera moves and eases to a stop. This one's a pure code fix, already
committed on this branch — this guide is mostly explaining what changed and
walking through how to verify it, not manual Editor setup like `12a`/`12c`.

---

## What this was

The visual glitch is usually called **tile seams** (or "seam flickering") —
thin sub-pixel gaps at the shared edge between two adjacent tiles, letting
whatever's behind them show through for a frame, flickering as the gap
shifts. Confirmed as a camera issue, not a tile-art one:
`Assets/Scripts/Camera/CameraFollow.cs` eases the camera toward the player
every frame with `Vector3.Lerp` (and eases zoom the same way), so the
camera's rendered position is almost never exactly aligned to the art's
pixel grid. Each tile is its own quad, so when the camera sits between pixel
boundaries, two neighboring tiles can round their shared edge in slightly
different directions — that's the gap. Since the Lerp keeps easing even
after you "stop" (it's exponential smoothing, not an instant snap), the gap
crawls around before settling, which reads as shimmer.

Your tile import settings (Point filtering, no compression, Full Rect mesh
— `12a`) already ruled out the other common cause (texture bleeding inside
the sprite sheet itself), which is what pointed at the camera specifically.

## The fix

Unity ships an official **Pixel Perfect Camera** component for exactly this
class of problem, but it wasn't the right fit here: it takes over
`Camera.orthographicSize` itself to guarantee an integer pixel scale, and
`CameraFollow.cs` also sets `orthographicSize` every frame for the
mouse-wheel zoom feature (§3.7) — the two would fight for control of the
same value every frame. This is a known, documented conflict (Unity even
ships a separate Cinemachine extension specifically to resolve it when
Cinemachine's involved) — not something worth taking on just to fix a
shimmer, and not something to layer in silently under a guide about
something else.

Instead, `CameraFollow.LateUpdate()` now rounds its own smoothed x/y to the
nearest actual on-screen pixel right before applying it:

```csharp
private Vector3 SnapToScreenPixel(Vector3 position)
{
    float worldUnitsPerScreenPixel = 2f * cam.orthographicSize / Screen.height;
    position.x = Mathf.Round(position.x / worldUnitsPerScreenPixel) * worldUnitsPerScreenPixel;
    position.y = Mathf.Round(position.y / worldUnitsPerScreenPixel) * worldUnitsPerScreenPixel;
    return position;
}
```

`worldUnitsPerScreenPixel` is computed from the *current* orthographic size
and screen height every frame, not a fixed constant — so it stays correct
whether you're fully zoomed in, fully zoomed out, or anywhere in between,
unlike a naive snap against the art's fixed 16 Pixels Per Unit (which would
only be exactly right at one specific zoom/resolution combination). Only
the camera is snapped, not the player/monster sprites — moving sprites
drifting at sub-pixel positions doesn't cause seams the way a grid of
touching static tiles does, so snapping the camera alone is enough to fix
what you were seeing.

---

## Step 1 — Pull and verify

1. **Fetch → Pull** in GitHub Desktop (or `git pull`).
2. Let Unity recompile, check the **Console** for zero red errors.
3. Press Play. Walk around, especially near the edge of the zoomed-in view
   where tile seams were most visible, and stop moving a few times.
4. Try it at a couple of zoom levels (scroll in near `minZoom`, scroll out
   near `maxZoom`) — the fix should hold at any zoom, not just the default.

No shimmering line should appear at tile edges anymore, while moving or
stopped.

---

## Step 2 — Save and commit

Nothing to hand-author in the Editor this time (no scene changes, no new
assets) — if `git status`/GitHub Desktop shows anything beyond your own
in-progress work, stop and check before committing. Otherwise there's
nothing new to commit from this step; the fix already came in on the pull.

---

## ✅ Checkpoint

- [ ] Pulled latest, zero Console errors
- [ ] Played and moved the camera around — no shimmering tile-edge line,
      while moving or after stopping
- [ ] Checked at both a zoomed-in and zoomed-out level, still clean

---

## Update — camera smoothness overhaul (supersedes the default above)

Pixel snapping turned out to cost more than it bought. Playtesting showed
jittery movement and a background that snapped around while zooming, and
this was the main cause: rounding the camera to whole pixels every frame
visibly *steps* slow movement instead of gliding, and can oscillate
between two adjacent pixels when the camera is nearly at rest. It's worse
while zooming, because the snap grid is computed from the current
orthographic size — so the grid itself resizes every frame mid-zoom.

**`Pixel Snap` is now an Inspector toggle on `CameraFollow`, defaulting
to OFF.** Everything this guide describes still works — flip it on if you
ever decide seams bother you more than stepping does. Two other camera
changes landed at the same time:

- **Zoom is now multiplicative and eased in log space.** Each wheel notch
  multiplies zoom by `Zoom Step Factor` (default `1.15` = 15% per notch)
  instead of adding a flat amount, so every notch feels identical at any
  zoom level. Scroll input is also read by actual amount rather than
  flattened to a fixed step, so trackpad scrolling zooms smoothly instead
  of in hard jumps.
- **Follow now requires Rigidbody2D interpolation** — see the required
  Editor step below.

### Required Editor step — turn on Rigidbody2D interpolation

This one is not code, and it's the single biggest jitter fix:

1. Select the **Player** GameObject in the `Game` scene.
2. On its **Rigidbody 2D** component, set **Interpolate** to
   **Interpolate** (it defaults to `None`).
3. Save the scene, and File → Save Project.

Why it matters: the player is moved by physics in `FixedUpdate`, which
runs at a fixed 50Hz, but the camera follows in `LateUpdate`, which runs
every rendered frame (60/120/144Hz). Without interpolation the camera is
chasing a position that only actually changes 50 times a second, so it
stutters no matter how good the easing in `CameraFollow` is. Interpolation
makes `transform.position` report a smoothed position between physics
steps, which fixes both the player's own rendering and the camera
following it.

### Tuning it to taste

All on `CameraFollow`, all live-editable in Play mode:

- **Zoom Step Factor** — how much one notch zooms (`1.15` = 15%; lower for
  finer control, higher for faster zooming).
- **Zoom Smoothing** — how quickly zoom eases to the new level. Higher =
  snappier, lower = floatier.
- **Follow Speed By Zoom** — the existing curve: how tightly the camera
  tracks the player, zoomed in (left) versus zoomed out (right).

---

## Notes for later

- **If you ever want true integer-pixel-perfect scaling** (every source
  pixel maps to an exact whole number of screen pixels, the crispest
  possible pixel-art look, not just "no seams" but zero sub-pixel blur on
  the art itself) — that's a bigger, separate change: Unity's official
  Pixel Perfect Camera package, combined with switching `CameraFollow`'s
  follow/zoom logic onto Cinemachine's Pixel Perfect extension (the
  supported way to combine smooth zoom with true pixel-perfect rendering).
  Worth its own guide if/when wanted; not needed for the shimmer problem
  this guide fixes.
- This is unrelated to the physics **"seam"** terminology already used in
  `guides/10-walls-gates-pathfinding.md` (colliders catching on the gap
  between adjacent wall tiles) — same word, different problem, already
  fixed there via Composite Collider 2D.
