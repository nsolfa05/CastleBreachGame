# Guide 13d — Rebindable controls & a controls screen

**Goal:** every keyboard control becomes rebindable from the Settings
menu, and there's a controls screen that explains the current keys (and
updates itself when you rebind them).

Numbered `13d` because `13c` is still reserved for its planned job
(win/lose "back to menu" + the full scene-transition pass). Same
out-of-sequence precedent as `09.5` and `12c`.

This guide assumes you've read `saving-and-committing.md` — follow that
checklist at the end as always, starting with the branch check.

---

## What's already done for you (this commit)

Every key in the game was hardcoded across six different scripts. They now
all route through one place, so a rebind takes effect everywhere at once.

**`Assets/Scripts/Systems/KeyBindings.cs`** — the single source of truth.
Scripts now ask *"was the Attack action pressed"* instead of *"was Space
pressed"*. Persisted in PlayerPrefs like `GameSettings`, and cached in
memory (movement is polled every frame — hitting PlayerPrefs that often
would be wasteful). Two kinds of binding:

- **Fixed actions** — Move Up/Down/Left/Right, Attack, Weapon Menu, Build
  Menu, Cancel, Restart.
- **Indexed slots** — weapon 1..N and building 1..N, deliberately *not*
  hardcoded as enum entries, because both lists grow as you add content.
  Adding a fifth weapon needs no code change here.

**`Assets/Scripts/UI/KeyRebindRow.cs`** — one row: a label, the current
key, and a button. Click, press any key, done.

**`Assets/Scripts/UI/KeyRebindMenu.cs`** — spawns all the rows from one
prefab, so you don't hand-place ~19 of them. Also has a
`OnResetToDefaultsPressed()` for a reset button.

**`Assets/Scripts/UI/ControlsDisplay.cs`** — fills a text object with a
formatted controls summary, read live from `KeyBindings`.

**Six gameplay scripts updated** to use it: `PlayerMovement`,
`PlayerAttack`, `ChargedWeapon`, `WeaponSwitcher`, `BuildModeController`,
`GameManager`. No behaviour change at default bindings — the game plays
exactly as before until you actually rebind something.

### Two design decisions worth knowing

- **Rebinding swaps, it doesn't clear.** Assign Space to Move Up while
  Space is Attack, and Attack takes Move Up's old key. The alternative —
  clearing the loser — silently leaves a control unusable, which is a much
  worse surprise than two controls trading places.
- **Escape cancels a rebind rather than being bindable.** A rebind prompt
  with no way out is a trap. The tradeoff is you can't move the Cancel
  action onto some other key from this UI.

---

## Step 1 — Pull and verify

1. **Fetch → Pull**, let Unity recompile, **Console shows zero errors.**
2. Press Play in `Game` and confirm the controls still work exactly as
   before — WASD, Space, V, B, number keys, Escape.

---

## Step 2 — Build the rebind row prefab

1. In the `Settings` scene, under the Canvas, create a **UI → Panel**,
   name it **`Row`**. Set its **Height** to about `40` and clear its
   background image if you don't want a visible box.
2. Add two **UI → Text - TextMeshPro** children: **`Label`** (left side)
   and **`KeyText`** (right side).
3. Add a **UI → Button - TextMeshPro** child named **`RebindButton`**.
   Position it over the right side. Delete its own child text object —
   `KeyText` is what displays the key, so the button just needs to be
   clickable. (Or keep the button's own text and use *that* as `KeyText`;
   either works.)
4. On `Row`, **Add Component → Key Rebind Row**. Assign:
   - **Label Text** ← the `Label` object
   - **Key Text** ← the `KeyText` object
   - **Rebind Button** ← the `RebindButton` object
   - Leave **Kind**, **Action**, and **Slot Index** alone — `KeyRebindMenu`
     overwrites them per row.
5. Drag `Row` into `Assets/Prefabs` to make it a prefab, then delete the
   copy from the Hierarchy.

## Step 3 — Build the rebind list

1. Under the Canvas, create an empty **UI → Panel** named
   **`ControlsList`**. Add a **Vertical Layout Group** component to it
   (that's what stacks the spawned rows), and a **Content Size Fitter**
   with **Vertical Fit: Preferred Size** if you want it to grow to fit.
2. Create an empty GameObject named **`KeyRebindMenu`**, **Add Component →
   Key Rebind Menu**. Assign:
   - **Row Prefab** ← the `Row` prefab from `Assets/Prefabs`
   - **Row Container** ← the `ControlsList` object
   - **Weapon Slot Count** — `4` (Sword, Bow, Hammer, Fire Staff)
   - **Build Slot Count** — `6` (however many entries
     `BuildModeController`'s Build Options list has)
3. Optionally add a **Reset Controls** Button and wire its **On Click ()**
   to `KeyRebindMenu.OnResetToDefaultsPressed`.

> **There will be ~19 rows.** Put `ControlsList` inside a **Scroll View**
> (`UI → Scroll View`, with `ControlsList` as the Content object) unless
> you want them running off the bottom of the screen.

## Step 4 — Add the controls explanation

1. Add a **UI → Text - TextMeshPro** somewhere with room — its own panel,
   a second tab, or below the rebind list. Name it **`ControlsText`**.
2. Create an empty GameObject named **`ControlsDisplay`**, **Add Component
   → Controls Display**, and assign **Target Text** ← `ControlsText`.
3. Leave **Include Mouse** and **Include Notes** checked — mouse controls
   aren't rebindable (aiming reads the pointer directly), but a controls
   screen that omitted them would be misleading.

It fills itself in on enable, so you'll see the text appear the moment you
press Play — no need to type anything into the text field yourself.

---

## Step 5 — Test

Press Play from `Title` → **Settings**:

- Every control is listed with its current key.
- Click a key button → it reads "Press a key…" → press a key → it updates.
- Press **Escape** while it's listening → cancels, key unchanged.
- Rebind something to a key already in use → the two **swap**, and both
  rows update immediately.
- **Reset Controls** puts everything back.
- The controls text lists the current keys, including any you changed.

Then go into the game (**Test** from the Title screen) and confirm your
rebound keys actually drive the game — move with whatever you bound to Move
Up, open the weapon menu with your new key, and so on. Quit and relaunch to
confirm the bindings persisted.

---

## Step 6 — Save and commit

Follow `saving-and-committing.md`: check your branch, File → Save, File →
Save Project, review the Changes tab, commit, push.

---

## ✅ Checkpoint

- [ ] Pulled, zero Console errors, default controls still work in-game
- [ ] `Row` prefab built and saved to `Assets/Prefabs`
- [ ] `KeyRebindMenu` populates the list on Play
- [ ] Rebinding works, Escape cancels, conflicting keys swap
- [ ] Reset Controls restores defaults
- [ ] Controls text lists current keys and updates after a rebind
- [ ] Rebinds survive quitting and relaunching
- [ ] Committed and pushed

## Notes for later

- **Adding a new weapon or structure** — bump **Weapon Slot Count** or
  **Build Slot Count** on `KeyRebindMenu`. No code changes; the slot
  binding system already handles up to 9 of each.
- **Adding a whole new bindable action** — one enum entry plus its default
  and label in `KeyBindings`, and it appears in both the rebind list and
  the controls screen automatically.
- **Mouse buttons aren't rebindable.** Left click places structures, right
  click cancels building, and aiming reads the pointer position directly
  (`PlayerAim`). Making those rebindable would mean routing mouse input
  through `KeyBindings` too — doable, just a bigger change than this guide,
  and worth doing when/if gamepad support arrives since that has the same
  requirement.
- **The in-game HUD doesn't show controls yet.** `ControlsDisplay` works on
  any TMP text object, so the same component can drive a pause-menu or
  in-game help panel whenever those exist — nothing new to write.
