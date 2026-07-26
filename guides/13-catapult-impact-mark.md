# Guide 13 — Catapult impact mark

**Goal:** A basic placeholder visual showing where a splash shot actually
landed — a circle sized to match Splash Radius, so you can see exactly how
far the Catapult's blast reached. Only appears on splash hits (the Archer's
single-target arrow doesn't need one — the hit itself is already obvious).

---

## Step 1 — Pull

GitHub Desktop → **Fetch** → **Pull**. Let Unity recompile; zero Console
errors expected.

## Step 2 — Wire the impact mark sprite

1. In `Assets/Prefabs`, open the **`CatapultStone`** prefab (the projectile
   the Catapult fires — from Guide 09 Step 3).
2. On its **Projectile** component, under the new **Impact mark
   [Placeholder] — optional** header:
   - **Impact Mark Sprite** ← the **`Circle`** sprite from `Assets/Sprites`
     (⊙ picker).
   - **Impact Mark Color** — defaults to a muted brown-gray (a "scorched
     ground" placeholder). Editable.
   - **Impact Mark Seconds** — how long it stays visible, default `0.4`.
   - **Impact Mark Sorting Order** — default `4`, a ground marker beneath
     structures/characters. Fine as-is.
3. Exit the prefab. (Leave `Arrow`'s own Impact Mark Sprite empty — the
   Archer has no splash, so nothing will show there anyway; it's gated on
   Splash Radius > 0.)

## Step 3 — Playtest

- Fire the Catapult into a cluster of zombies — right where the stone lands,
  a circle appears sized to match the actual splash radius (Catapult's is
  `1`, so a 2-tile-wide circle), then disappears after Impact Mark Seconds.
- Confirm anything standing inside that circle at impact is the same set
  that actually took splash damage — the mark is drawn at exactly the
  radius the game checks.
- Raise **Splash Radius** on the Catapult's Attack Tower temporarily (e.g.
  to `2`) and confirm the mark grows to match.

## Step 4 — Commit

`Catapult impact mark: placeholder splash-radius circle at the landing point`

---

## ✅ Checkpoint

- [ ] CatapultStone's Impact Mark Sprite wired to Circle
- [ ] A circle appears at the Catapult's landing point, sized to Splash Radius
- [ ] It disappears after Impact Mark Seconds
- [ ] Archer's single-target arrow shows no mark (Splash Radius is 0)
- [ ] Committed & pushed
