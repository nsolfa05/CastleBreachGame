# Guide 11c — Combat UI: health bars, gold-on-death, selection UI, death effect

- 11a — Core framework + knockback/stun ✅
- 11b — Weapons (sword rework, bow, hammer, fire staff) + the `V` weapon menu ✅
- **11c — Combat UI (health-bars-on-damage, gold-on-death, selection-mode
  indicator, death effect)** ← you are here
- 11d — New enemies (Faun, Redcap)
- 11e — Oil & Flame tower (+ click-to-select-a-tower system)

Four pieces this round — weapon aim-preview "ghost visuals" (Bow/Hammer/Fire
Staff) were explicitly punted to later, so this sub-guide is:

1. **Health bars hide until damaged.** Monsters and towers already have an
   always-visible `HealthBar` (`Assets/Prefabs/HealthBar.prefab`). It now has
   a **Hide Until Damaged** toggle — off (default) keeps the old
   always-visible behavior everywhere it's not turned on; turning it on for
   the Monster and tower prefabs means a full-health one shows no bar at all,
   and taking damage reveals it for a few seconds before it fades back out.
2. **Gold loss on death**, configurable per your request: a dropdown on
   `PlayerRespawn` — **Lose All**, **Lose Percentage**, or **Lose Fixed
   Amount** — so you can flip between them and test without touching code.
3. **Weapon menu stays open + a top-left selection indicator.** Pressing a
   number while the `V` weapon menu is open now switches weapons WITHOUT
   closing the menu — press 1, 2, 3, 4 as many times as you like; only Esc
   (or `V` again) closes it. A new text readout, top-left, shows **"Weapon
   Choice"** or **"Building Choice"** while the respective menu is open.
4. **Death Effect.** A shared component for the Player and any monster: the
   body tints red and lingers for a bit before disappearing, while a burst of
   small square particles scatters outward from it.

---

## What the code adds (already written & pushed)

- `Combat/HealthBar.cs` — new **Hide Until Damaged** + **Visible After Damage
  Seconds** fields. Reads `Health.LastDamageTime` directly (not the `Damaged`
  event) so a monster's spawn-time stat setup never falsely flashes its bar.
- `Player/PlayerRespawn.cs` — new **Gold Loss Mode** dropdown (Lose All /
  Lose Percentage / Lose Fixed Amount) applied the instant you die; also now
  plays the Death Effect (if attached) and waits for it before hiding the
  body — see Step 5.
- `Systems/GameManager.cs` — new `RemoveGold(amount)`, floored at 0.
- `Player/WeaponSwitcher.cs` — pressing a number while the menu is open no
  longer auto-closes it; only Esc / pressing `V` again does.
- `Systems/HUD.cs` — new **Selection Mode Text** field, showing "Weapon
  Choice" / "Building Choice" / blank.
- `Combat/DeathEffect.cs` + `Combat/DeathParticle.cs` — the death visual: a
  red body tint (restored afterward, so a Skeleton's bone-pile revive or the
  player's respawn never comes back permanently red) plus a hand-rolled burst
  of small square sprite particles (no Unity ParticleSystem — same
  no-prefab, `new GameObject()` convention as `ImpactMark`/`BurnZone`).
- `Enemies/MonsterAI.cs` — a dead monster now freezes in place immediately
  (physics + colliders off, movement/attacking stopped via a new
  `health.IsDead` check) and its `Destroy()` is delayed by however long the
  Death Effect reports back, instead of destroying the instant it dies. Wave
  bookkeeping (the `Killed` event) still fires immediately, unaffected.
- New Editor foldout wrappers for `HealthBar`, `PlayerRespawn`, and
  `DeathEffect` (same collapsible sections as everything else since 11a).

---

## Step 1 — Pull & let Unity compile

Pull the latest `main`, let Unity recompile, confirm no red errors in the
Console.

---

## Step 2 — Turn on Hide Until Damaged for monsters and towers

1. **Project ▸ Assets/Prefabs**, double-click **`Monster`** to open it in
   Prefab Mode.
2. Find its **Health Bar** component → **Hide until damaged (Guide 11c)** →
   tick **Hide Until Damaged**. Leave **Visible After Damage Seconds** at `3`
   (or tune to taste).
3. Save the prefab (**Ctrl/Cmd+S**).
4. Repeat the same (open prefab → Health Bar → tick Hide Until Damaged) for
   each tower: **`ArcherTower`**, **`PikeTower`**, **`CatapultTower`**,
   **`PraiseTheKingTower`**.
5. **File ▸ Save Project.**

> Leaving it unticked anywhere keeps that bar exactly as it's always
> behaved — always visible. Nothing forces this on; it's opt-in per prefab.

---

## Step 3 — Set up gold loss on death

1. Select **Player**, find **Player Respawn**.
2. Under **Gold loss on death (Guide 11c)**, pick a **Gold Loss Mode**:
   - **Lose All** — death wipes your gold to 0.
   - **Lose Percentage** — set **Gold Loss Percentage** (e.g. `0.5` = lose
     half, keep half).
   - **Lose Fixed Amount** — set **Gold Loss Fixed Amount** (e.g. `50` —
     always lose exactly that much, floored at 0 if you're carrying less).
3. Pick whichever you want to test first — it's a dropdown, so switching
   later takes one click, no rewiring.

---

## Step 4 — Add the top-left selection indicator

1. Open your HUD **Canvas** in the Hierarchy.
2. Right-click it → **UI ▸ Text - TextMeshPro** (same kind of object as
   your existing Gold/Wave/King HP text). Name it something like
   **`SelectionModeText`**.
3. In its **Rect Transform**, anchor it to the **top-left** and position it
   there (drag the anchor preset in the top-left of the Rect Transform
   inspector, then nudge X/Y so it doesn't overlap anything else).
4. Select whichever object has the **HUD** component, and drag
   `SelectionModeText` into its new **Selection Mode Text** field.
5. It starts blank — that's correct, nothing's selected yet.

---

## Step 5 — Add the Death Effect component

1. Select **Player** → **Add Component ▸ Death Effect**.
2. **Project ▸ Assets/Prefabs**, double-click **`Monster`** to open it in
   Prefab Mode → **Add Component ▸ Death Effect** there too (shared by every
   monster type — Zombie, Cyclops, all of them).
3. On **both**, set **Particle Sprite** → `Assets/Sprites/Square`.
4. Everything else (**Death Tint**, **Body Lifetime Seconds**, **Particle
   Count**, **Knockback Amount**, **Particle Gravity**, **Particle Size**,
   **Particle Lifetime Seconds**, **Particle Color**) already has reasonable
   defaults — tune to taste once you've seen it in action (Step 6).
5. **Save** the Monster prefab and **File ▸ Save Project**.

> **Heads up:** the body-lingering time (**Body Lifetime Seconds**) counts
> *toward* `Player Respawn`'s **Respawn Delay**, not on top of it. If you set
> Body Lifetime Seconds longer than Respawn Delay, the corpse effectively
> floors how soon you respawn. Keep Respawn Delay ≥ Body Lifetime Seconds
> unless that's the effect you want.

---

## Step 6 — Playtest

1. Press **Play**. Look at a full-health monster or tower — no health bar
   should be visible.
2. Hit one (sword, arrow, tower fires on a monster, a monster hits a tower)
   — its bar should appear immediately, then fade out again a few seconds
   after the last hit if nothing else damages it.
3. A monster right at low health but not recently hit should also be
   hidden — this is purely time-since-last-hit, not "is it damaged" as a
   permanent state. Hit it again to bring the bar back.
4. Note your current **Gold** (HUD), then let the player die. Confirm the
   gold changes exactly as your chosen **Gold Loss Mode** describes the
   instant you die (before the respawn delay finishes) — try all three modes
   at least once.
5. **Weapon menu:** press **V**, top-left text should read **"Weapon
   Choice"**. Press 1, then 2, then 3 — the weapon should switch each time
   and the menu should stay open the whole time. Press **Esc** — text clears,
   menu closes.
6. **Building menu:** press **B** and pick up a structure — top-left text
   should read **"Building Choice"**. Confirm you still can't open the
   weapon menu while carrying a ghost, and can't start building while the
   weapon menu is open (both already blocked since the last round of fixes).
7. **Death Effect — monster:** kill a monster. Its body should flash red,
   a burst of small squares should scatter outward from it, and the corpse
   should sit motionless (no more walking/attacking) for **Body Lifetime
   Seconds** before disappearing.
8. **Death Effect — player:** let the player die. Same red tint + particle
   burst; the player's body should stay visible (frozen, unable to move or
   attack) for the linger time before vanishing and respawning.
9. **Skeleton check:** if you have one in a test wave, kill it once — it
   should still go into its normal bone-pile revive (squashed, its own bone
   pile color) rather than getting stuck red-tinted or disappearing early.
10. Confirm nothing else regressed: sword/weapon switching, health bars on
    anything you left **Hide Until Damaged** OFF for (should still be
    always-visible, e.g. if you skipped a tower).

---

## ✅ Checkpoint

- [ ] Console has no errors after pulling
- [ ] Monster prefab's Health Bar: **Hide Until Damaged** ✔
- [ ] All four tower prefabs' Health Bars: **Hide Until Damaged** ✔
- [ ] Full-health monsters/towers show no bar; taking damage reveals it, then
      it fades a few seconds after the last hit
- [ ] Player Respawn has a **Gold Loss Mode** set, tested in all three modes
      (Lose All / Lose Percentage / Lose Fixed Amount) at least once
- [ ] Gold is removed the instant the player dies, not after the respawn delay
- [ ] `SelectionModeText` created, anchored top-left, wired into HUD
- [ ] Pressing V then multiple number keys switches weapons repeatedly
      without closing the menu; Esc closes it
- [ ] Top-left text correctly shows "Weapon Choice" / "Building Choice" /
      blank, and the two never show at the same time
- [ ] Player and Monster prefab both have **Death Effect**, Particle Sprite
      set to Square
- [ ] Monster and player deaths show the red tint + particle burst, and the
      body lingers for Body Lifetime Seconds before disappearing
- [ ] A dead monster stops moving/attacking immediately, even before its
      corpse visually disappears
- [ ] Skeleton's bone-pile revive still works normally after this change
- [ ] **File ▸ Save Project**, committed & pushed (verified on github.com)

---

## Notes for later

- **Weapon aim-preview "ghost visuals"** (a Bow range line, a Hammer strike
  box, a Fire Staff range + burn-radius preview — matching the Sword's
  always-visible grey/yellow crescent from the last round of 11b fixes) were
  explicitly deferred, not forgotten — say the word whenever you want them
  built the same way.
- **Player health bar**: you didn't ask for a world-space bar above the
  player itself (only monsters/towers) — the player currently has no
  on-screen HP readout at all (`HUD.cs` shows King HP and Gold, not Player
  HP). Flag if you want that added later; it's a small, separate piece.
- **`HealthBar.LastDamageTime`-based gating** means healing (if it's ever
  added) won't hide the bar early — it'll still show until the timer runs
  out. Worth revisiting once healing exists, not a concern with anything in
  the game today.
- **Death Effect is one shared set of values** on the Monster prefab — every
  monster type currently gets the identical particle count/color/knockback
  (only the Player's own Death Effect can differ). If you want, say, a
  Cyclops to burst bigger/differently-colored debris than a Zombie, that
  would need `MonsterDefinition` to carry its own override fields (like
  `bodyColor`/`bonePileColor` already do) — not built now since you only
  asked for one shared effect.
- **Hand-rolled particles, not Unity's ParticleSystem** — deliberate, to
  match this project's existing convention (`ImpactMark`, `BurnZone`,
  `ChargeIndicator` all avoid it) and to keep the Inspector fields simple and
  purpose-named instead of exposing Unity's full (and much more complex)
  Particle System module stack.
