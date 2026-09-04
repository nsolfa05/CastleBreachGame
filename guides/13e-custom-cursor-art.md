# Guide 13e — Custom cursor art, alignment, and the cursor setting

**Goal:** swap the placeholder circle for real cursor art, aligned so the
part of the sprite that *looks* like the click point actually sits on the
click point. Also resolves what happens to the Cursor Speed setting, which
is now dormant.

Numbered `13e` because `13c` is still reserved for its planned job
(win/lose "back to menu" + the scene-transition pass), and `13d` is the
rebindable-controls guide. Same out-of-sequence precedent as `09.5`/`12c`.

The cursor itself was built in `13a` Step 2 — this guide picks up from
there and doesn't repeat it.

---

## What changed since 13a (already committed)

`CustomCursor` originally eased toward the pointer at
`GameSettings.CursorSpeed`. That was wrong on its own terms, not just
sluggish: **aiming and building read the raw mouse position** (`PlayerAim`,
`BuildModeController`), so a smoothed cursor drew itself somewhere other
than where the player was actually aiming or placing a structure. An
accuracy bug, not a feel preference.

It now pins to the pointer exactly, every frame, in `LateUpdate`, with no
smoothing at all. Nothing to tune — there is no lag left to dial out.

While confirming that, one claim in `13a` turned out to be wrong and has
been corrected there: the Cursor Speed slider's max of `50` was described
as "effectively instant." At 60fps it closed only ~57% of the gap per
frame, so even maxed it visibly trailed. The whole range was short of what
it claimed.

---

## The settings side — what to do with the Cursor Speed slider

Since the cursor no longer smooths, that slider stores a value nothing
reads. Three options, all defensible:

1. **Leave it** — design doc §2 asks for a cursor speed setting, and it's
   the natural home for a gamepad-driven cursor later, where a speed value
   genuinely means something (a stick gives a direction, not a position).
   Costs nothing but a control that currently does nothing.
2. **Hide it** — uncheck the Slider GameObject's active checkbox in the
   `Settings` scene. Re-enable it when gamepad support lands. This is the
   recommended option: a visible control that does nothing reads as a bug
   to anyone testing the game.
3. **Delete it** — also fine. `GameSettings.CursorSpeed` stays in code
   either way and would just need a new slider wired to it later.

If you hide or delete it, also remove its label text so you don't leave an
orphaned "Cursor Speed" heading above nothing.

---

## Step 1 — Import the cursor sprite

1. Drop your cursor art into `Assets/Sprites/UI` (create the folder if
   needed).
2. In the Inspector: **Texture Type: Sprite (2D and UI)**.
3. **Filter Mode** — `Point (no filter)` if it's pixel art, `Bilinear` if
   it's a painterly/smooth image. Same rule as the backgrounds.
4. Leave the sprite's own **Pivot** import setting alone. It governs
   `SpriteRenderer` and world-space sprites and is **ignored** by UI
   `Image` — the RectTransform pivot in Step 3 is what actually matters
   here. This is an easy hour to lose.

## Step 2 — Point the cursor at it

1. Open the `CursorCanvas` prefab (`Assets/Prefabs`).
2. Select its **`Cursor`** child, and set the **Image** component's
   **Source Image** to your new sprite.
3. Set the RectTransform's **Width/Height** to the sprite's real pixel
   dimensions, or an exact multiple of them (e.g. `32 × 32` for a 32×32
   sprite, or `64 × 64` to draw it at 2×). This matters for Step 3 — the
   pivot is a *fraction* of this rectangle, so a mismatched size puts your
   computed pivot on the wrong part of the art.
4. Confirm **Raycast Target** is still unchecked (from `13a`). The cursor
   sits under the pointer at all times, so with it checked it intercepts
   every UI click meant for whatever's underneath.

## Step 3 — Align the tip (the important part)

`CustomCursor` sets `rectTransform.position = mouse.position`, and a
RectTransform places its **pivot** at that position. So the pivot *is* the
cursor's hotspot: whatever fraction you set is the part of the sprite that
lands exactly on the real pointer.

The placeholder circle used pivot `(0.5, 0.5)` — correct, since a circle's
click point is its middle. Most cursor art is not like that.

| Cursor shape | Pivot |
|---|---|
| Crosshair / dot / circle | `0.5, 0.5` |
| Classic arrow, tip at top-left | `0, 1` |
| Arrow pointing up, tip at top-centre | `0.5, 1` |
| Tip anywhere else | compute it below |

For a tip at pixel `(tipX, tipY)` measured from the sprite's **top-left**
corner:

```
pivotX = tipX / spriteWidth
pivotY = 1 - (tipY / spriteHeight)
```

The `1 -` is because RectTransform pivot Y counts up from the bottom while
image pixels count down from the top. Example: a 32×32 arrow whose tip is
at pixel (4, 2) → pivot `(0.125, 0.9375)`.

Set this on the **`Cursor`** child's RectTransform (the Pivot field is
right there in the Inspector) — not on the Canvas.

## Step 4 — Verify it's actually aligned

Eyeballing this is unreliable. Compare against the real pointer directly:

1. In `CustomCursor.cs`, temporarily comment out the `Cursor.visible =
   false;` line in `LateUpdate()`.
2. Press Play. You'll see the OS arrow *and* your sprite at once.
3. Move around and check the two tips coincide. Adjust the pivot until
   they do.
4. **Restore the line** when you're done.

A functional check too: hover the very edge of a button. It should
highlight exactly when your cursor's tip touches it, not before or after.

**Note:** clicks were always accurate — nothing reads the custom cursor's
position, so a misaligned pivot never actually broke clicking. What it
broke was the visual telling you the truth about where you were clicking.

---

## Update — reliable OS-cursor hiding, and an in-game skin/size picker

Three gaps found after this guide originally shipped.

**The OS cursor could reappear and stay stuck.** `Cursor.visible = false`
was only ever called once, in `Awake()`. Losing/regaining window focus,
alt-tabbing, or a scene transition's `OnDisable` (which does the opposite,
deliberately, so the Editor's own cursor comes back when you stop Play)
could all bring the real arrow back with nothing left to hide it again.
Fixed by moving that line into `LateUpdate()`, so it's re-asserted every
single frame instead of once — whatever brought the OS cursor back gets
immediately overridden the very next frame.

**There was no way to change the cursor sprite, or its size, without
opening Unity.** `CustomCursor` now holds a list of **`CursorSkin`**
entries — a sprite, its own pivot (different shapes need different
hotspots), and its own **Base Size** (different sprites need different
proportions, or a shared fixed size would stretch whichever one it wasn't
authored for) — and Settings gets a dropdown to pick between them plus a
slider that scales whichever one is active, both persisted like every
other setting.

### Required Editor steps

**1. Add skins to the `CursorCanvas` prefab:**

1. Open `Assets/Prefabs/CursorCanvas.prefab`, select the **`Cursor`**
   child, find the **Custom Cursor** component.
2. Expand **Skins**. Add one entry for what's already there — **Display
   Name** `"Circle"` (or whatever you're calling it), **Sprite** ← the
   sprite currently on the Image component, **Pivot** ← whatever pivot
   you already worked out in Steps 1-4 above, **Base Size** ← the
   RectTransform's current Width/Height (e.g. `16, 16`).
3. Add another entry per additional cursor you want selectable — your
   dagger, say. Same four fields each: name, sprite, *that sprite's own*
   pivot (recompute per Steps 2-4, don't reuse the circle's), and **Base
   Size** matching *that* sprite's real aspect ratio. This matters: a UI
   Image stretches to fill whatever size it's given, so if the dagger's
   Base Size doesn't match its own proportions, switching to it will
   visibly squash or stretch it — even though the circle looked fine.
4. Index `0` is what shows before the player ever opens Settings — put
   whichever skin you want as the default first.

**2. Add the dropdown and size slider to `Settings.unity`:**

1. Under the Canvas, add **UI → Dropdown - TextMeshPro**, name it
   `CursorSkinDropdown`, with a label above it same as the other settings.
2. Add a **UI → Slider** named `CursorSizeSlider`, with a label. Set its
   **Min Value** to something like `0.25` and **Max Value** to `3` — below
   `1` shrinks the current skin below its authored Base Size, above `1`
   enlarges it.
3. On **`SettingsMenu`**, assign **Cursor Skin Dropdown** ← the dropdown,
   **Cursor Size Slider** ← the slider.
4. Wire the Dropdown's **On Value Changed (Int32)** → `SettingsMenu` →
   `OnCursorSkinChanged`.
5. Wire the Slider's **On Value Changed (Single)** → `SettingsMenu` →
   `OnCursorSizeChanged`.

You don't need to type the skin names into the Dropdown yourself —
`SettingsMenu.Start()` reads them from `CustomCursor.Skins` and populates
the list automatically, so the two can never disagree about what skins
exist.

### Test

- Open `Settings`: the dropdown lists every skin by name, showing the
  currently-saved one selected; the size slider shows the saved scale.
- Pick a different skin: the cursor **in the Settings scene itself**
  changes immediately (Settings has its own `CustomCursor` instance, same
  prefab as everywhere else) — you're previewing the real thing, not a
  mockup. It should show that skin's own aspect ratio correctly, not
  stretched.
- Drag the size slider: the cursor shrinks/grows live, no need to reopen
  anything.
- Switch skins again after resizing: the new skin also comes in at your
  chosen scale, not reset to 1×.
- Go to `Game`: same skin and size show there too.
- Quit and relaunch: both choices persisted.
- Alt-tab away and back, or click off the game window and back, while
  playing — the OS arrow should never reappear and stick.

---

## Update — a Hide OS Cursor toggle, and an automatic missing-cursor check

Two more gaps, both from the same underlying question: what if a player
(or a scene) doesn't want the custom cursor hiding the real one?

**There was no way to turn off custom-cursor hiding.** Until now,
`CustomCursor` always hid the OS arrow and showed its own sprite — no
opt-out. `GameSettings` gets a new `HideOsCursor` bool (default **on**,
persisted like everything else). `CustomCursor.LateUpdate()` now reads it
every frame: on, the OS arrow stays hidden and the sprite tracks the
pointer, same as before; off, the OS arrow shows and the sprite disables
itself instead — never both at once, since one drawn on top of the other
reads as a bug, not a choice.

**There was no way to tell, just by looking at the Console, whether a
scene had actually wired up its cursor.** This project has hit that exact
failure more than once — a scene missing the `CursorCanvas` prefab
entirely, or carrying a corrupted instance of it — with the only symptom
being "the real arrow is showing" and no clue why. A new script,
`CursorPresenceCheck.cs`, runs itself automatically at startup (via
`[RuntimeInitializeOnLoadMethod]` — no GameObject, no Inspector wiring)
and again every time a scene loads. If `Hide OS Cursor` is on but that
scene has no `CustomCursor` anywhere in it, it logs a `Debug.LogWarning`
naming the scene, so the Console tells you immediately instead of you
having to notice the arrow yourself and guess.

### Required Editor steps

**Add the toggle to `Settings.unity`:**

1. Under the Canvas, add **UI → Toggle**, name it `HideOsCursorToggle`,
   with a label like "Hide OS Cursor" next to it.
2. On **`SettingsMenu`**, assign **Hide Os Cursor Toggle** ← the toggle.
3. Wire the Toggle's **On Value Changed (Boolean)** → `SettingsMenu` →
   `OnHideOsCursorChanged`.

Nothing else to wire — `CursorPresenceCheck` needs no setup at all; it's
active the moment the script is in the project.

### Test

- Open `Settings`: the toggle shows the saved state (on by default).
- Turn it off: the OS arrow appears immediately and the custom sprite
  disappears — in the Settings scene itself, live, same as the skin/size
  controls.
- Turn it back on: reverses immediately.
- Go to `Game`/`Campaign`/`Title`: the same on/off state applies there
  too, and persists after quitting and relaunching.
- To see the missing-cursor check fire: temporarily disable the
  `CursorCanvas` instance in any one scene (or just its `CustomCursor`
  component) with the toggle left on, then enter Play. The Console should
  log a warning naming that scene. Re-enable it afterward.

---

## Step 5 — Save and commit

Follow `saving-and-committing.md`: check your branch first, File → Save,
File → Save Project, review the Changes tab (expect the new sprite + its
`.meta`, the `CursorCanvas` prefab, the `CursorPresenceCheck.cs` script,
and the `Settings` scene), commit, push.

---

## ✅ Checkpoint

- [ ] Cursor sprite imported with the right Filter Mode for its art style
- [ ] `CursorCanvas` prefab points at it, Width/Height match the sprite
- [ ] Raycast Target still off
- [ ] Pivot set so the tip sits on the real pointer, verified against the
      OS cursor with the hide line temporarily commented out
- [ ] `Cursor.visible = false;` restored afterwards
- [ ] Cursor Speed slider hidden, deleted, or deliberately left in place
- [ ] `Skins` populated on `CursorCanvas`'s Custom Cursor, each with its
      own correct pivot AND Base Size matching its sprite's aspect ratio
- [ ] `CursorSkinDropdown` wired on `SettingsMenu`, lists every skin,
      switching one previews live in the Settings scene itself
- [ ] `CursorSizeSlider` wired, resizes live, and stays correct across a
      skin switch
- [ ] Skin and size choices persist after quitting and relaunching
- [ ] OS cursor stays hidden through alt-tab / focus loss and regain
- [ ] `HideOsCursorToggle` wired on `SettingsMenu`; turning it off shows
      the real arrow and hides the custom sprite (never both at once)
- [ ] Hide Os Cursor choice persists after quitting and relaunching
- [ ] Disabling a scene's `CustomCursor` (with the toggle on) logs a
      `CursorPresenceCheck` warning naming that scene
- [ ] Committed and pushed

## Notes for later

- **Per-state cursors** (a build-mode cursor, an aiming reticle, a
  different look over UI) are easy from here: swap the Image's sprite at
  runtime, and set the pivot to match that sprite's own hotspot at the
  same time. Two sprites with different tips need two different pivots —
  changing only the sprite will silently misalign it.
- **Animated cursors** work too, since this is an ordinary UI Image —
  an Animator, or just swapping sprites on a timer.
- **If the ~1 frame of software-cursor latency ever bothers you:** a
  Canvas-drawn cursor is composited with the rest of the frame, so during
  fast flicks it can trail the hardware pointer slightly. That's inherent
  to the approach, not leftover smoothing. `Cursor.SetCursor(texture,
  hotspot, CursorMode.Auto)` replaces the actual OS cursor bitmap and has
  literally zero latency — and takes the hotspot directly in **pixels**
  rather than as a pivot fraction. The cost is it's no longer a scene
  object you can scale, tint, animate, or drive with shaders, which is why
  it wasn't the default choice here.
