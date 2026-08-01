# Guide 11b — Weapons

- 11a — Core framework + knockback/stun ✅
- **11b — Weapons (sword rework, bow, hammer, fire staff) + the `V` weapon
  menu** ← you are here
- 11c — Combat UI (ghost previews, health-bars-on-damage) + gold-on-death
- 11d — New enemies (Faun, Redcap)
- 11e — Oil & Flame tower (+ click-to-select-a-tower system)

This gives the player **four weapons** — Sword, Bow, Hammer, Fire Staff — and
a **`V` menu** to switch between them (mirrors `B` for building: press `V`,
press `1`–`4` to pick one, `V` again or `Esc` to close without changing).
Exactly one weapon is ever active; switching, dying, and respawning all route
through one place (`WeaponSwitcher`) instead of each weapon managing itself.

**Three judgment calls I made without asking first — flagged here, easy to
change if they're not what you had in mind (see Notes at the bottom for
where in the code each one lives):**

1. **Charge release timing.** Releasing Space *before* the weapon's full wind-up
   completes cancels the attempt — no shot, no cooldown penalty. Releasing at
   or after full charge fires immediately. Holding past full charge costs
   nothing; only the release moment matters once you're past it.
2. **Fire Staff's "2 tiles wide" burn patch** = a 1-tile *radius* (2-tile
   diameter) circle, not a literal 2×2 square.
3. **`B`/`V` can't both be open at once** — opening one now force-ignores the
   other's hotkey that frame, so a stray number key while carrying a building
   ghost can't also swap your weapon (and vice versa). You didn't ask for
   this explicitly, but it was a one-line guard on each side and avoids a real
   bug (both menus reading the same 1–4 keys at once).

---

## What the code adds (already written & pushed)

**New:**
- `Player/PlayerAim.cs` — single source of "which way is the player aiming"
  (mouse direction), read by all four weapons. Pulled out of `PlayerAttack`,
  which used to track the mouse itself.
- `Player/ChargedWeapon.cs` — shared "hold Space to charge, release to fire"
  base class for Bow, Hammer, Fire Staff (the Sword still swings instantly,
  no charge).
- `Combat/ChargeIndicator.cs` — the wind-up visual: an outline box with a fill
  bar that grows as you hold, tracking the player and your aim live.
- `Combat/StraightProjectile.cs` — fixed-direction flight for the Bow's arrow
  and Fire Staff's bolt (distinct from the existing homing `Projectile` used
  by towers).
- `Combat/BurnZone.cs` — a ground patch that ticks damage over time; the Fire
  Staff's landing burn, built generic enough that the Oil & Flame tower
  (11e) can reuse it later instead of new fire code.
- `Player/BowWeapon.cs`, `Player/HammerWeapon.cs`, `Player/FireStaffWeapon.cs`
  — the three charged weapons.
- `Player/WeaponSwitcher.cs` — owns which weapon is active; the `V` menu.
- Editor foldout wrappers for all four new components (same collapsible
  sections as everything else since 11a).

**Changed:**
- `Player/PlayerAttack.cs` (the Sword) — the hit-test is now a real **arc**
  centered on you (was a rectangle offset ahead of you), and it reads
  `PlayerAim` instead of tracking the mouse itself. Same damage/cooldown/
  reach/obstruction/knockback fields as before.
- `Enemies/MonsterAI.cs` — added `NotifyForcedPlayerEngagement()`, called by
  the Hammer on every hit so a slam pulls a monster's attention onto you even
  without knockback/stun turned on (the "distract" part of your brief).
- `Player/PlayerRespawn.cs` — death/respawn now disables/restores whichever
  weapon is equipped via `WeaponSwitcher`, instead of hardcoding `PlayerAttack`.
- `Structures/BuildModeController.cs` — ignores its own hotkeys while the
  weapon menu is open (see judgment call #3 above).

---

## Step 1 — Pull & let Unity compile

1. Pull the latest `main`.
2. Let Unity recompile, open **Window ▸ General ▸ Console**, confirm **no red
   errors**.

---

## Step 2 — Re-wire the aim pivot onto the new Player Aim component

`PlayerAttack` used to track the mouse and rotate an **Aim Pivot** child
Transform itself; that's moved to the new `PlayerAim` component, so it needs
its reference set again.

1. Select **Player**. Note whatever Transform was previously dragged into
   **Player Attack ▸ Aim Pivot** — that field is gone from Player Attack now.
2. **Add Component ▸ Player Aim**.
3. Drag that same child Transform into **Player Aim ▸ Aim Pivot**.

---

## Step 3 — Give each charged weapon its own charge-bar visual

Bow, Hammer, and Fire Staff each need their **own** `Charge Indicator` (don't
share one — they can be switched independently and a shared bar would show
the wrong weapon's charge).

1. Under **Player**, create three empty children: right-click Player ▸
   **Create Empty**, rename to `BowChargeBar`; repeat for `HammerChargeBar`
   and `FireStaffChargeBar`.
2. On each, **Add Component ▸ Charge Indicator**.
3. Set **Box Sprite** → `Assets/Sprites/Square` on all three (same flat square
   used elsewhere for placeholder boxes). Leave the outline/fill colors at
   their defaults for now — tune later.

---

## Step 4 — Add the three new weapons to the Player

1. Select **Player**. **Add Component ▸ Bow Weapon**.
   - **Hit Layers** → tick **Enemy**.
   - **Charge Indicator** → drag in `BowChargeBar`.
   - Leave **Arrow Effects** off (matches the design brief — no knockback/stun
     on the bow) unless you want it.
2. **Add Component ▸ Hammer Weapon**.
   - **Hit Layers** → **Enemy**. **Obstruction Layers** → **Structure** +
     **King** (same wall-block rule as the Sword).
   - **Charge Indicator** → drag in `HammerChargeBar`.
   - **Hammer Effects** → optionally turn on Knockback (a heavy slam suits a
     shove) — your call.
3. **Add Component ▸ Fire Staff Weapon**.
   - **Hit Layers** → **Enemy**.
   - **Charge Indicator** → drag in `FireStaffChargeBar`.
   - Burn fields (**Damage Per Tick 2**, **Tick Interval 1**, **Duration 6**,
     **Burn Radius 1**) are pre-filled with reasonable defaults — leave them
     for now.

Don't worry that Bow/Hammer/Fire Staff all show as "enabled" in the Inspector
right now — `WeaponSwitcher` (next step) forces only one active the moment you
press Play.

---

## Step 5 — Add the Weapon Switcher

1. Still on **Player**, **Add Component ▸ Weapon Switcher**.
2. Expand **Weapons**, set **Size → 4**, and fill in, **in this exact order**
   (index = the number key that selects it):
   - **Element 0** — Display Name `Sword`, Component → drag **Player Attack**.
   - **Element 1** — Display Name `Bow`, Component → drag **Bow Weapon**.
   - **Element 2** — Display Name `Hammer`, Component → drag **Hammer Weapon**.
   - **Element 3** — Display Name `Fire Staff`, Component → drag **Fire Staff
     Weapon**.

**Save Project.**

---

## Step 6 — Playtest

1. Press **Play**. You should start equipped with the **Sword** (spacebar
   swings immediately, same as before — just a real arc now instead of a
   box).
2. **Weapon menu:** press **V** — nothing visible changes yet (11c adds UI),
   but pressing **2** should switch you to the **Bow** (spacebar now charges
   instead of swinging). Press **V** then **1** to go back to the Sword.
   Press **V** then **Esc** — menu should close with no weapon change.
3. **Bow:** hold Space, watch the charge bar fill in front of you, release
   after it's full — an arrow flies straight and damages the first enemy it
   hits. Release **early** (before the bar fills) — no arrow, no cooldown.
4. **Hammer:** switch to it (V, 3), hold and release fully — everything in
   the box in front of you takes damage. Test the distract effect: let a
   monster commit to attacking a wall/tower, then hammer-slam it — it should
   immediately turn and come for you.
5. **Fire Staff:** switch to it (V, 4), fire at a monster (or empty ground) —
   a burning patch should appear wherever the bolt landed and tick damage for
   a few seconds.
6. **Mid-charge weapon switch:** start charging the Bow, then press V and
   switch to Hammer before releasing — the Bow's charge bar should vanish and
   no arrow should fire later.
7. **Death/respawn:** die while a weapon (any of them) is equipped — combat
   should fully disable, and on respawn you should come back with the *same*
   weapon still equipped and working.
8. **B/V exclusivity:** press B to carry a building ghost, then press V —
   nothing should happen (menu doesn't open). Press Esc/B to put the ghost
   away, then V should work normally again.

---

## ✅ Checkpoint

- [ ] Console has no errors after pulling
- [ ] Player has **Player Aim** with **Aim Pivot** re-wired
- [ ] Three `ChargeIndicator` children created, Box Sprite = Square, each
      assigned into its matching weapon
- [ ] Player has **Bow Weapon**, **Hammer Weapon**, **Fire Staff Weapon**, each
      with Hit Layers = Enemy (Hammer also Obstruction Layers = Structure+King)
- [ ] Player has **Weapon Switcher** with all 4 slots filled in order
      (Sword, Bow, Hammer, Fire Staff)
- [ ] Sword still swings as before, now with a visible arc gizmo when selected
- [ ] V opens/closes the weapon menu; 1–4 switch weapons; Esc/V-again closes
      without switching
- [ ] Bow charges, fires a straight arrow, damages on hit; early release cancels
      for free
- [ ] Hammer's slam damages an area and pulls a distracted monster's attention
      onto the player
- [ ] Fire Staff leaves a burning patch that ticks damage over time
- [ ] Switching weapons mid-charge cleanly cancels the old charge
- [ ] Dying disables combat entirely; respawning restores the weapon you had
- [ ] Carrying a building ghost blocks the weapon menu from opening, and vice versa
- [ ] **File ▸ Save Project**, committed & pushed (verified on github.com)

---

## Notes for later

- **Judgment call #1 (release timing)** lives in `ChargedWeapon.Update()` —
  the `if (Time.time - chargeStartTime >= windUpTime)` check. Flip the logic
  there if you'd rather a weapon always fire at whatever charge fraction it
  has when released.
- **Judgment call #2 (burn radius)** is `FireStaffWeapon.burnRadius` — bump it
  if you want a bigger patch, or if "2 tiles wide" should read as radius 2
  instead of 1.
- **Judgment call #3 (B/V exclusivity)** is the one-line guards in
  `BuildModeController.Update()` (checks `WeaponSwitcher.SelectingWeapon`) and
  `WeaponSwitcher.Update()` (checks `BuildModeController.BuildingActive`) —
  delete either if you'd rather they not interact at all.
- **`BurnZone` is deliberately generic** (radius/tick/damage/duration/layers
  all passed in at spawn) so 11e's Oil & Flame tower can reuse it for its
  flame tiles instead of new DoT code — it does NOT yet support "bonus damage
  while standing in it" or "lingers after leaving," since nothing needs those
  yet.
- **The Sword's arc is still the cheap line-of-sight version** from 11a, now
  drawn as a true wedge instead of a box — an expanding-hitbox visual that
  physically stops at a wall is still a "later" idea if you want it.
