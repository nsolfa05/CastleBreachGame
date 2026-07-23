# Guide 07 — Phase 1: Monster stats as ScriptableObjects

**Goal:** Monster stats move out of the prefab and into **ScriptableObjects** —
designer-editable asset files, one per monster type. One generic `Monster`
prefab plays as ANY monster depending on which definition asset it's handed.
The WaveSpawner learns mixed, multi-group waves. After this guide the game
plays exactly like before — same zombies, same waves — but adding a brand-new
monster type becomes "create one asset file, type in numbers" with zero code.

You'll learn: ScriptableObjects (Unity's designer-data files) — one of the
most important concepts in real Unity projects.

> **Heads-up before pulling:** this update REPLACES the old `ZombieAI` script
> with the generic `MonsterAI`. After you pull, the Zombie prefab will show a
> **"Missing Script"** warning in its Inspector — that's expected, Step 3
> fixes it. Nothing else breaks.

---

## Step 1 — Pull and understand what a ScriptableObject is

1. GitHub Desktop → **Fetch origin** → **Pull origin**. Let Unity recompile;
   the Console should show no red errors.
2. Concept, in one paragraph: a **ScriptableObject** is an asset file that
   holds *data* — like a prefab holds an object. Our new `MonsterDefinition`
   holds every §7.3 stat (speed, HP, damages, ranges, drops) plus behavior
   switches (revives? ignores the player? prioritizes structures?). The
   monster prefab stops *owning* stats and starts *reading* them from
   whichever definition it's given. This is exactly the design doc's §1 rule:
   new content without touching code.

## Step 2 — Create the Zombie definition asset

1. In the Project panel, right-click `Assets` → **Create → Folder** →
   name it **`Monsters`**.
2. Open it, right-click inside → **Create → Castle Breach → Monster
   Definition** (our new menu — this is the ScriptableObject!). Name it
   **`Zombie`**.
3. Select it and look at the Inspector — every stat from the design doc's
   §7.3 Zombie column is already the default: speed 4, HP 10, damage 3 every
   1.5s, notices the player within 6 tiles, drops 3 gold. Nothing to change.
   (Set **Body Color** to your preferred zombie green if you like.)

## Step 3 — Turn the Zombie prefab into the generic Monster prefab

1. In `Assets/Prefabs`, **double-click the `Zombie` prefab** (Prefab Mode).
2. In the Inspector you'll see the old component slot saying **"Missing
   Script"** (the deleted `ZombieAI`). Click its **⋮ menu → Remove Component**.
3. **Add Component → Monster AI**. Wire it:
   - **Definition** ← the `Zombie` asset from `Assets/Monsters`
   - **Currency Drop Prefab** ← the `CurrencyCoin` prefab
   - **Structure Layers** → check **Structure**
   - **Body** ← leave empty (it auto-uses the sprite on this object)
4. Exit Prefab Mode (the `<` arrow).
5. In the Project panel, rename the prefab (**F2**) from `Zombie` to
   **`Monster`** — it's no longer zombie-specific. (Renaming keeps all
   references intact.)

## Step 4 — Rebuild the WaveSpawner

The wave format changed shape (waves are now lists of *spawn groups*), so the
old saved settings can't carry over — cleanest is a fresh component:

1. Select the **`WaveSpawner`** object in the Hierarchy.
2. On the old **Wave Spawner** component: **⋮ → Remove Component**.
3. **Add Component → Wave Spawner** (fresh, with new defaults). Wire:
   - **Map** ← the `MapGenerator` object
   - **Monster Prefab** ← the `Monster` prefab
4. Expand **Waves** — three starter waves are pre-filled, each containing
   **groups**. A group = *which monster* × *how many* × *which gate* × *spawn
   speed* × *start delay*. One thing isn't pre-filled (code can't reference
   asset files): **each group's `Monster` field is empty.** Expand every
   group and set **Monster** ← the `Zombie` definition asset. (7 groups total
   across the three waves.)
5. **Ctrl+S.**

## Step 5 — Play and verify nothing changed

Press Play. It should feel identical to before: zombies pour from gates in
three waves, chase you inside 6 tiles, drop coins, damage the King, and the
HUD's wave counter works. If a group was left without a Monster assigned,
the Console logs a warning and skips it — that's your tell.

**The payoff test:** stop Play, select `Assets/Monsters/Zombie`, change
**Move Speed** to `8`, press Play — every zombie is now fast. One asset edit
changed every monster of that type, no prefab or code touched. Set it back
to 4. *That's* what this phase was for.

## Step 6 — Commit

`Phase 1: monster stats in ScriptableObjects, mixed-group waves`

---

## ✅ Checkpoint

- [ ] `Assets/Monsters/Zombie` asset exists with §7.3 stats
- [ ] Prefab renamed `Monster`, runs `MonsterAI`, no Missing Script slot
- [ ] WaveSpawner uses groups; all groups reference the Zombie asset
- [ ] Game plays exactly as before; speed-tweak test worked
- [ ] Committed & pushed

**Next:** [Guide 08 — The monster roster](08-monster-roster.md), where this
setup pays off: four new monsters, zero new code.
