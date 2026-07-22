# Guide 00 — Install Unity & create the project

**Goal:** Unity is installed, the `CastleBreach` project exists inside this repo, the
starter scripts compile with zero errors, and everything is committed to git.

Time estimate: 45–60 minutes (most of it is download/install time).

---

## Step 1 — Get the repo onto your computer

You need a local copy of the `CastleBreachGame` repo. If you've only ever worked on
the repo through Claude sessions, the friendliest way is **GitHub Desktop**:

1. Download and install [GitHub Desktop](https://desktop.github.com/), sign in with
   your GitHub account.
2. **File → Clone Repository**, pick `nsolfa05/CastleBreachGame`, choose where to
   put it (e.g. `Documents/CastleBreachGame`), click **Clone**.
3. You should now see a `guides` folder and a `unity-scripts` folder directly inside
   the repo folder on your disk. Everything lives on the `main` branch — no branch
   switching needed.

<details>
<summary>Command-line alternative (if you already use git in a terminal)</summary>

```bash
git clone https://github.com/nsolfa05/CastleBreachGame.git
cd CastleBreachGame
```
</details>

---

## Step 2 — Install Unity Hub and Unity

1. Download **Unity Hub** from <https://unity.com/download> and install it.
2. Open Unity Hub and sign in / create a free Unity account (choose the free
   **Personal** license when asked).
3. In the Hub's left sidebar click **Installs → Install Editor**.
4. Install the newest version marked **LTS** (Long-Term Support) — any **Unity 6 LTS**
   (version numbers look like `6000.x.x`) is right for this project.
5. When asked about extra modules, you don't need any yet — skip build supports and
   documentation to save disk space. Click **Install** and let it finish (it's large;
   this takes a while).

---

## Step 3 — Create the Castle Breach project

1. In Unity Hub click **Projects → New project**.
2. At the top, make sure the Unity 6 LTS version you just installed is selected.
3. Pick the **Universal 2D** template (older Hub versions call it **2D (URP)**).
   This matches the design doc's confirmed decision: 2D + URP.
4. Set:
   - **Project name:** `CastleBreach` (exactly this — one word, capital C and B)
   - **Location:** the root of your cloned repo, e.g. `Documents/CastleBreachGame`
   Unity will create the project at `.../CastleBreachGame/CastleBreach`. The repo's
   `.gitignore` is already set up for exactly that path.
5. Click **Create project** and wait — first-time project creation takes a few
   minutes. Unity opens when it's done.

---

## Step 4 — A 5-minute tour of the editor

Before touching anything, learn the five panels you'll use constantly:

| Panel | Where (default layout) | What it is |
|---|---|---|
| **Scene view** | Center | Your working view of the game world. Right-drag or hold middle-mouse to pan, scroll to zoom. |
| **Game view** | Tab next to Scene | What the player sees. Activates when you press Play. |
| **Hierarchy** | Left | Every object in the current scene, as a tree. |
| **Project** | Bottom | Every file (asset) in the project — scripts, sprites, prefabs. This mirrors the `Assets` folder on disk. |
| **Inspector** | Right | The details of whatever you've selected. Almost all "editing" happens here. |

And the two most important controls:

- The **Play button** (▶ top-center) runs the game. Press it again to stop.
  ⚠️ **Changes you make while in Play mode are thrown away when you stop** — a classic
  beginner trap. Always stop Play mode before editing.
- **Ctrl+S** (Cmd+S on Mac) saves the current scene. Save often.

Also open the **Console** now (Window → Panels → Console, or Ctrl+Shift+C) and keep it
docked next to the Project panel — it's where errors appear.

---

## Step 5 — Bring in the starter scripts

The repo ships all the C# code for the vertical slice in `unity-scripts/` at the
repo root. Move it into the project:

1. In Unity's **Project** panel, click the `Assets` folder.
2. Open your file explorer to `CastleBreachGame/unity-scripts`.
3. Drag the whole `unity-scripts` **folder** from the file explorer into the empty
   area of Unity's Project panel (inside `Assets`). Unity copies and imports it.
4. In the Project panel, single-click the newly imported `unity-scripts` folder,
   press **F2** (or click its name again) and rename it to **`Scripts`**.
5. Wait a few seconds for the compile spinner (bottom-right) to finish, then check
   the **Console**: there should be **zero red errors**. (Yellow warnings are fine.)
6. Back in your file explorer, **delete the original** `unity-scripts` folder from
   the repo root — the copy inside `CastleBreach/Assets/Scripts` is now the one
   true home of the code. This avoids two drifting copies.

> **If you see red errors mentioning `InputSystem`:** the scripts use Unity's Input
> System package, which the Universal 2D template normally includes. Fix: **Window →
> Package Manager**, dropdown top-left set to **Unity Registry**, search
> "Input System", click **Install**. If Unity then asks to enable the new input
> backend and restart, say yes.

---

## Step 6 — Commit your work

Git-wise, a Unity project generates a lot of junk (a `Library` folder, logs, IDE
files). The repo's `.gitignore` already excludes all of it — what gets committed is
`Assets/`, `Packages/`, `ProjectSettings/`, and the small `.meta` files Unity
creates next to every asset. **Always commit the `.meta` files** — Unity uses them
to keep references between assets working.

In GitHub Desktop:

1. You'll see a big list of changed files (the new `CastleBreach` project, plus the
   deleted `unity-scripts` folder). That's expected.
2. Summary: `Create Unity project and import vertical slice scripts`
3. Click **Commit to main**, then **Push origin**.

<details>
<summary>Command-line alternative</summary>

```bash
git add -A
git commit -m "Create Unity project and import vertical slice scripts"
git push -u origin main
```
</details>

---

## ✅ Checkpoint

- [ ] Unity Hub + a Unity 6 LTS editor installed
- [ ] `CastleBreach` project exists at `CastleBreachGame/CastleBreach`
- [ ] `Assets/Scripts` contains the nine code folders (Camera, Combat, Core, Economy, Enemies, Map, Player, Structures, Systems)
- [ ] Console shows zero errors
- [ ] Committed and pushed

**Next:** [Guide 01 — Build the castle map](01-castle-map.md)
