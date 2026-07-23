# Guide 06 — Tower attack-range circle

**Goal:** See a translucent circle showing the Archer Tower's attack range —
both on the ghost while placing, and by left-clicking any already-placed tower
(click anywhere else to hide it again).

The code is already in the repo (`TowerRangeCircle.cs`); this is just one
component to add and one field to wire.

---

## Step 1 — Add the component to the tower prefab

1. Pull the latest changes (GitHub Desktop → Fetch → Pull), wait for Unity's
   compile spinner, confirm zero Console errors.
2. In the Project panel, **double-click the `ArcherTower` prefab** to open
   Prefab Mode.
3. **Add Component → Tower Range Circle**.
4. Wire its one required field: **Circle Sprite** ← the `Circle` sprite from
   `Assets/Sprites` (use the ⊙ picker).
5. Exit Prefab Mode (the `<` back arrow) — it saves automatically.

## Step 2 — Test

Press Play:

- Press **B** — the ghost now carries a big translucent circle showing exactly
  what the tower will cover from that spot. Move it around and watch the
  coverage shift.
- Place a tower, exit build mode (right-click/Esc/B), then **left-click the
  placed tower** — its range circle toggles on. Click empty ground — it hides.
  Clicking a different tower shows that one's circle instead.

Tunables on the Tower Range Circle component: **Circle Color** (including how
transparent it is) and **Sorting Order** (default 10 — above the map, beneath
coins and characters).

## Step 3 — Save & commit

`Tower range circle on ghost and placed towers`

---

**Next:** the post-slice build order lives in [`ROADMAP.md`](../ROADMAP.md) —
Phase 1 (monster stats → ScriptableObjects) is up first.
