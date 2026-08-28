# Guide 13a — Title scene, Settings, and a custom cursor

**Goal:** the first thing you see on launch is a Title scene with Campaign /
Survival (button only, §2) / Test / Settings buttons, a working Settings
scene (Master Volume, Cursor Speed), and a custom on-screen cursor used
everywhere instead of the OS pointer. First guide of Phase 6 — no dependency
on the tile/camera work from `12a`–`12d`.

This guide assumes you've read `saving-and-committing.md` already — follow
that checklist at the end as always.

---

## What's already done for you (this commit)

Four new scripts, ready to wire up in the Editor — nothing else in the
project needed to change:

- **`Assets/Scripts/Systems/GameSettings.cs`** — Master Volume and Cursor
  Speed, persisted via `PlayerPrefs`. Volume applies live through
  `AudioListener.volume`, which works today even with zero sound effects in
  the project yet — it'll just start actually doing something the moment
  real audio is added later, no rework needed.
- **`Assets/Scripts/UI/TitleMenu.cs`** — Campaign/Test/Settings button
  handlers. Survival has no handler; its Button stays non-interactable per
  "button only."
- **`Assets/Scripts/UI/SettingsMenu.cs`** — wires the two sliders to
  `GameSettings` and a Back button to the Title scene.
- **`Assets/Scripts/UI/CustomCursor.cs`** — the custom cursor. Full
  reasoning is in its class comment; the short version: it hides the OS
  pointer and eases a UI Image toward the real mouse position at
  `GameSettings.CursorSpeed`. This is deliberately future-proofed for a
  gamepad-driven cursor later (position integrated from stick input instead
  of eased toward a target) but doesn't build any gamepad support now — none
  exists elsewhere in the project yet either.

**Nothing about existing gameplay changes.** `PlayerAim`/`BuildModeController`
still read the raw mouse directly, same as always — the custom cursor is a
visual layer on top, not a rewire of how aiming or building placement works.

---

## Step 1 — Pull and verify

1. **Fetch → Pull**, let Unity recompile, check the **Console** for zero
   errors.

---

## Step 2 — Build the cursor prefab (once, reused everywhere)

1. Hierarchy → right-click → **UI → Canvas**. Rename it **`CursorCanvas`**.
   On its **Canvas** component, set **Render Mode** to **Screen Space -
   Overlay** (should already be the default) and **Sort Order** to **100**
   — high enough to always draw above the HUD or any menu Canvas.
2. Right-click `CursorCanvas` → **UI → Image**. Rename it **`Cursor`**.
3. On the `Cursor` Image: set **Source Image** to the existing `Circle`
   sprite (`Assets/Sprites`). Set its **Width/Height** small, e.g. `16, 16`.
   Pick any color that'll stand out (Image's **Color** field) — this is
   placeholder art like everything else, swap it later.
4. **Critical — uncheck Raycast Target on the `Cursor` Image.** It sits
   exactly under the pointer at all times; left checked, it intercepts
   every UI click meant for whatever button is underneath it, and nothing
   would be clickable anymore.
5. Select `Cursor`, **Add Component → Custom Cursor**.
6. Drag `CursorCanvas` (with its `Cursor` child) from the Hierarchy into
   `Assets/Prefabs` to make it a **Prefab**. You'll drop this exact prefab
   into every scene from here on, including Campaign (`13b`) and Game
   later — one edit to the prefab updates it everywhere.
7. Delete the instance from the Hierarchy now that it's a prefab (you'll
   re-add it per-scene below) — or leave it, since this scene gets
   overwritten anyway in the next step.

---

## Step 3 — Build the Title scene

1. **File → New Scene** (or right-click in `Assets/Scenes` → whatever your
   Unity version's flow is), **Basic (Built-in)** template. **File → Save
   As** → `Assets/Scenes/Title.unity`.
2. Hierarchy → **UI → Canvas** (name it `TitleCanvas` is fine, default is
   too). Add an **EventSystem** if Unity doesn't prompt to create one
   automatically alongside the Canvas (it usually does).
2a. On the Canvas's **Canvas Scaler** component: set **UI Scale Mode** to
   **Scale With Screen Size**, **Reference Resolution** to `1920 x 1080`,
   **Screen Match Mode** to **Match Width Or Height**, **Match** `0.5`.
   Without this, the default **Constant Pixel Size** mode makes every UI
   element a fixed pixel size regardless of the actual window/resolution —
   fine at one specific size, inconsistent at any other. Do this on every
   Canvas you build from here on (Settings below, and Campaign in `13b`).
   To preview accurately while you work: Game view's aspect ratio dropdown
   (top-left of that tab) defaults to **Free Aspect**, which just stretches
   to fill the Editor panel — switch it to a fixed ratio like **16:9**, or
   add an exact resolution (`+` in that dropdown) matching `1920x1080`, so
   what you see actually represents a real player's screen.
3. Under the Canvas, add **four Buttons** (`UI → Button - TextMeshPro`),
   stacked vertically, labeled (via each Button's child TMP text)
   **Campaign**, **Survival**, **Test**, **Settings**.
4. On the **Survival** Button component specifically: uncheck
   **Interactable**. That's the entire "button only" requirement — no
   script handler needed for it.
5. Create an empty GameObject, name it **`TitleMenu`**, **Add Component →
   Title Menu**. Leave its three scene-name fields at their defaults
   (`Campaign`, `Game`, `Settings`) — they already match what these scenes
   will be named.
6. Wire the three active buttons' **On Click ()** (bottom of the Button
   component in the Inspector, click `+`): drag the `TitleMenu` object in,
   then pick `TitleMenu.OnCampaignPressed` / `OnTestPressed` /
   `OnSettingsPressed` for Campaign/Test/Settings respectively.
7. Drag the `CursorCanvas` prefab from `Assets/Prefabs` into the Hierarchy.

---

## Step 4 — Build the Settings scene

1. **File → New Scene** → **File → Save As** → `Assets/Scenes/Settings.unity`.
2. **UI → Canvas** — set its Canvas Scaler the same way as Step 3.2a above
   (Scale With Screen Size, 1920x1080, Match Width Or Height, 0.5). Then
   under it add: two **Sliders** (`UI → Slider`) with a TMP label above
   each (`Master Volume`, `Cursor Speed`), and a **Back** Button
   (`UI → Button - TextMeshPro`, rename it `Back`, change its child TMP
   text to say "Back").
3. **Master Volume** Slider — Inspector: **Min Value 0**, **Max Value 1**.
4. **Cursor Speed** Slider — Inspector: **Min Value 2**, **Max Value 50**.
   **This setting is now dormant — see the note at the bottom.** The
   cursor tracks the mouse exactly, so this slider stores a value but has
   no visible effect until gamepad support exists. Leave it in place (the
   design doc §2 calls for it) or hide it for now — your call.
5. Empty GameObject, name it **`SettingsMenu`**, **Add Component →
   Settings Menu**. Drag the two Sliders into its **Volume Slider**/
   **Cursor Speed Slider** fields. Leave **Title Scene Name** at its
   default (`Title`).
6. Wire each Slider's **On Value Changed (Single)**: drag `SettingsMenu`
   in, pick `SettingsMenu.OnVolumeChanged` (Volume slider) /
   `OnCursorSpeedChanged` (Cursor Speed slider).
7. Wire the **Back** Button's On Click → `SettingsMenu.OnBackPressed`.
8. Drag the `CursorCanvas` prefab in here too.

---

## Step 5 — Register both scenes (and Game) in Build Settings

`SceneManager.LoadScene("SomeName")` only works for scenes actually listed
here — miss this step and every button throws a clear "scene couldn't be
loaded" error.

1. **File → Build Settings** (or **File → Build Profiles** depending on
   your Unity version) → **Add Open Scenes**, or drag all three scene
   assets in from `Assets/Scenes`.
2. Order: **`Title` first (index 0)** — that's what plays when you press
   Play or launch a build — then `Settings`, then `Game`.
3. Close the window.

---

## Step 6 — Test end-to-end

Open `Title`, press Play:

- **Test** → loads `Game`, the existing vertical slice plays exactly as
  before.
- **Settings** → loads `Settings`; drag both sliders, then **Back** →
  returns to `Title`. Reopen `Settings` — both sliders should remember
  where you left them (that's the `PlayerPrefs` persistence working).
- **Survival** — button visibly present, does nothing when clicked
  (non-interactable). Expected.
- **Campaign** — will throw a Console error ("Scene ... couldn't be
  loaded"). **Expected until `13b`** builds that scene — not a bug.
- Everywhere: the OS arrow pointer should be invisible, replaced by the
  small circle cursor, pinned exactly to where the real pointer is with no
  visible lag.

---

## Step 7 — Save and commit

Follow `saving-and-committing.md`: File → Save, File → Save Project, check
GitHub Desktop's Changes tab (expect the four new scripts + `.meta`s, the
new `Title.unity`/`Settings.unity` scenes + `.meta`s, the new
`CursorCanvas` prefab + `.meta`, and the Build Settings change), commit,
push.

---

## ✅ Checkpoint

- [ ] Pulled latest, zero Console errors
- [ ] Cursor prefab built, Raycast Target off, saved as a Prefab in
      `Assets/Prefabs`
- [ ] Title scene: 4 buttons, Survival non-interactable, Campaign/Test/
      Settings wired
- [ ] Settings scene: both sliders wired, values persist across Back →
      reopen
- [ ] All three scenes added to Build Settings, `Title` at index 0
- [ ] Tested Test/Settings/Survival/Campaign per Step 6 — Campaign's error
      is expected, everything else works
- [ ] Custom cursor visible and trailing correctly in both scenes
- [ ] Committed and pushed

## Notes for later

- **`13b`** builds the actual Campaign scene (scrollable, zigzagging,
  named, moveable level nodes on a splined dashed trail) — that's what
  makes the Campaign button work.
- **`13c`** wires the win/lose screens' "back to menu" and does a full
  scene-transition pass once Campaign exists too.
- **Sound is fully wired, just silent** — `AudioListener.volume` responds
  to the slider right now, there just aren't any audio clips playing yet
  anywhere in the project. Nothing to redo when real sound effects show up.
- **Cursor Speed is dormant, and that's deliberate.** The cursor
  originally eased toward the pointer at this speed, which meant the
  visible cursor sat *behind* where you were actually aiming — `PlayerAim`
  and `BuildModeController` both read the raw mouse position, so easing
  the visual made it disagree with real aim/placement. That's an accuracy
  bug, not a style choice, so `CustomCursor` now pins to the pointer
  exactly, every frame, with no smoothing at all. The setting still saves
  a value and is reserved for a future gamepad-driven cursor, where speed
  genuinely means something (a stick gives a direction, not a position).
- **Residual latency, if you ever chase it:** a Canvas-drawn cursor is a
  *software* cursor, so it's composited with the rest of the frame and can
  still trail the hardware pointer by about a frame during fast flicks —
  that's inherent to the approach, not leftover smoothing. If that ever
  matters, `Cursor.SetCursor()` swaps the actual OS cursor bitmap and has
  literally zero latency, at the cost of not being a scene object you can
  scale, tint, animate, or drive with shaders.
