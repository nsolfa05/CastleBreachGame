# Guide 11c — Combat UI: health bars on damage, gold-on-death

- 11a — Core framework + knockback/stun ✅
- 11b — Weapons (sword rework, bow, hammer, fire staff) + the `V` weapon menu ✅
- **11c — Combat UI (health-bars-on-damage) + gold-on-death** ← you are here
- 11d — New enemies (Faun, Redcap)
- 11e — Oil & Flame tower (+ click-to-select-a-tower system)

Two pieces this round — weapon aim-preview "ghost visuals" (Bow/Hammer/Fire
Staff) were explicitly punted to later, so this sub-guide is just:

1. **Health bars hide until damaged.** Monsters and towers already have an
   always-visible `HealthBar` (`Assets/Prefabs/HealthBar.prefab`). It now has
   a **Hide Until Damaged** toggle — off (default) keeps the old
   always-visible behavior everywhere it's not turned on; turning it on for
   the Monster and tower prefabs means a full-health one shows no bar at all,
   and taking damage reveals it for a few seconds before it fades back out.
2. **Gold loss on death**, configurable per your request: a dropdown on
   `PlayerRespawn` — **Lose All**, **Lose Percentage**, or **Lose Fixed
   Amount** — so you can flip between them and test without touching code.

---

## What the code adds (already written & pushed)

- `Combat/HealthBar.cs` — new **Hide Until Damaged** + **Visible After Damage
  Seconds** fields. Reads `Health.LastDamageTime` directly (not the `Damaged`
  event) so a monster's spawn-time stat setup never falsely flashes its bar.
- `Player/PlayerRespawn.cs` — new **Gold Loss Mode** dropdown (Lose All /
  Lose Percentage / Lose Fixed Amount) + the percentage/amount fields it
  reads from, applied the instant you die (before the respawn delay).
- `Systems/GameManager.cs` — new `RemoveGold(amount)`, floored at 0.
  Unconditional (unlike `TrySpendGold`) — losing gold on death can't be
  "refused" the way a purchase can.
- New Editor foldout wrappers for `HealthBar` and `PlayerRespawn` (same
  collapsible sections as everything else since 11a).

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

## Step 4 — Playtest

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
5. Confirm nothing else regressed: sword/weapon switching, health bars on
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
