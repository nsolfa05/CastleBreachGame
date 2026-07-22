# Guide 04 — Currency & HUD

**Goal:** Zombies drop bobbing gold coins (§4) that despawn after 8 seconds; you
collect them by walking over them; and an on-screen HUD shows gold, wave progress,
King health, and the real VICTORY / GAME OVER messages with **R to restart**.

You'll learn: trigger colliders, editing prefabs in Prefab Mode, and Unity's UI
system (Canvas + TextMeshPro).

---

## Step 1 — The coin prefab

1. Hierarchy → **2D Object → Sprites → Circle**, name it **`CurrencyCoin`**. Set:
   - **Scale**: `0.3, 0.3, 1`
   - **Color**: yellow/gold
   - **Order in Layer**: `15` (above the map, below characters)
2. **Add Component → Circle Collider 2D**, and check its **Is Trigger** box.
   ("Trigger" = detects overlaps but doesn't physically block — the knight walks
   *over* the coin, not *into* it.)
3. **Add Component → Currency Drop** (our script). The §4 numbers are pre-set:
   8-second despawn, gentle bob — both tunable here, as the doc requires.
4. Drag `CurrencyCoin` into `Assets/Prefabs`, then **delete it from the Hierarchy**.

## Step 2 — Wire the coin into the Zombie prefab

The zombie in the *scene* is gone — so this edit happens on the *prefab*:

1. In `Assets/Prefabs`, **double-click** the `Zombie` prefab. The view switches to
   **Prefab Mode** (blue breadcrumb bar at the top — you're now editing the
   template itself).
2. Select the root `Zombie`, find **Zombie AI → Currency Drop Prefab**, and drag
   the `CurrencyCoin` prefab from the Project panel into it.
   (**Currency Drop Amount** is 3 — the §7.3 zombie value.)
3. Click the **←** arrow in the breadcrumb (or the `<` next to "Zombie") to exit
   Prefab Mode. Changes save automatically.

## Step 3 — The HUD

1. Hierarchy → right-click → **UI → Text - TextMeshPro**. The first time, Unity
   asks to **Import TMP Essentials** — click it, wait, close the window. You now
   have a `Canvas` with a text child (plus an `EventSystem` — leave it be).
2. Select the `Canvas`: in **Canvas Scaler**, set **UI Scale Mode** to
   **Scale With Screen Size** (so the HUD looks the same at any window size).
3. Rename the text child to **`GoldText`** and set it up:
   - With `GoldText` selected, look at the **Rect Transform**. Click the **anchor
     square** (top-left of the component, looks like a crosshair target) and,
     **while holding Shift+Alt**, click the **top-left** preset. The text jumps to
     the screen's top-left corner.
   - Set **Pos X** `20`, **Pos Y** `-20` for a small margin.
   - Text: `Gold: 0`, **Font Size** `28`, Color: gold. (The script overwrites the
     text; you're just styling.)
4. Duplicate `GoldText` three times (**Ctrl+D**) and adjust each:
   - **`WaveText`** — anchor preset **top-center** (Shift+Alt), Pos `0, -20`,
     **Alignment: Center**.
   - **`KingText`** — anchor preset **top-right**, Pos `-20, -20`,
     **Alignment: Right**. ⚠️ If the text overflows leftward oddly, widen its
     **Width** to ~300.
   - **`MessageText`** — anchor preset **middle-center**, Pos `0, 0`, **Font
     Size** `48`, **Alignment: Center**, Color: white, **Width** ~800, **Height**
     ~200. Clear its text content (it only shows at win/lose).
5. Select the `Canvas` → **Add Component → HUD** (our script). Wire:
   - **Gold Text** ← `GoldText`, **Wave Text** ← `WaveText`,
     **King Health Text** ← `KingText`, **Message Text** ← `MessageText`
   - **Wave Spawner** ← the `WaveSpawner` object

## Step 4 — Play!

- Kill a zombie → a gold coin pops out, bobbing. Walk over it → the gold counter
  ticks up by 3.
- Ignore a coin for 8 seconds → it vanishes (tune that on the coin prefab).
- Top bar shows `Get ready...` → `Wave 1 / 3` etc., and the King's health live.
- Let the King die → **GAME OVER — press R** … and R actually restarts now.
- Clear all three waves → **VICTORY!**

## Step 5 — Save & commit

`Currency drops and HUD with win/lose messaging`

---

## ✅ Checkpoint

- [ ] Zombies drop bobbing coins; walking over them raises the gold counter
- [ ] Coins despawn after 8s if ignored
- [ ] HUD shows gold (top-left), wave (top-center), King HP (top-right)
- [ ] Win and lose both show their message, and **R** restarts the level

**Next:** [Guide 05 — Archer Tower & build mode](05-archer-tower-and-build-mode.md) — the last slice piece!
