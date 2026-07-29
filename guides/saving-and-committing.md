# Saving & Committing — the checklist to run every single time

This isn't a one-time setup guide like the numbered ones — it's a reference
to reopen **every time** you finish Editor work in Unity, before you touch
git at all. Every past incident where work "vanished" (the full monster
roster, Guide 09's towers, and twice now with Guide 10's walls/gates) traced
back to skipping a step on this list, never to git itself doing something
wrong.

## The one thing to understand first

Unity has **two different kinds of "saved"**, and only one of them is
obvious:

| What you did | When it reaches disk |
|---|---|
| Created a new **prefab** (dragged a GameObject into `Assets/Prefabs`) | **Immediately** |
| Changed **Project Settings** (Tags and Layers, Physics 2D matrix, etc.) | **Immediately** |
| Created/moved/edited a **GameObject in the Hierarchy** (the scene) | **Only on File → Save Project** |
| Added/edited a **component on an existing scene object** (e.g. a Tilemap's colliders, `PathGrid`'s fields, `BuildModeController`'s Build Options list) | **Only on File → Save Project** |

The dangerous part: an unsaved scene change looks *completely correct* the
whole time — the Inspector shows it, Play Mode uses it, nothing looks wrong.
The only sign anything's missing is that the `.unity` file on disk (and
therefore git) never changed. That's why this has bitten us more than once —
there's no visible warning, only a silent gap.

## The checklist

Run through this every time, in order, no skipping:

1. **Finish your Editor changes.**
2. **File → Save Project** — from the menu bar, not Ctrl+S. Do this even if
   you're *sure* nothing you touched was scene-level; it costs nothing and
   removes the guesswork.
3. **Open GitHub Desktop's Changes tab before committing anything.** Compare
   what's listed against what you actually did:
   - New prefab/script/asset → should appear as a new file.
   - New/edited GameObject, component, or Inspector list on something in the
     scene → `Game.unity` should be listed as modified.
   - **If something you did isn't listed here, stop.** Don't commit yet —
     go back, figure out what didn't save (usually: repeat step 2), and
     re-check this tab before moving on. Committing at this point would
     silently leave that piece out, which is exactly how this keeps
     happening.
4. **Write a commit message that says what you actually did**, then commit.
5. **Push.**
6. **Confirm the push landed** — either check the commit shows up on
   github.com yourself, or tell me you pushed and I'll `git fetch` and
   check the real commit hash on GitHub. Don't assume a push worked just
   because GitHub Desktop didn't show an error — if your local branch was
   behind (see below), it'll refuse and ask you to fetch/merge first, which
   is fine and normal, just don't stop partway through.

## Keeping things organized

- **New prefabs belong in `Assets/Prefabs`** (structures, monsters) — that's
  the existing convention; don't create new subfolders ad hoc without a
  reason, since guides and this checklist assume that layout.
- **Every asset needs its `.meta` file committed alongside it.** GitHub
  Desktop stages both automatically when you commit through it — this has
  only ever gone wrong when files were added some other way. If you ever see
  a file in `Assets/` with no matching `.meta` in the same commit, flag it.
- **Don't redo a step that's already done.** If you're following a guide
  again after a gap (a fetch/pull, a break, switching machines), check what
  already exists in the Hierarchy/Project window first — recreating an
  object that's already there (a second `PathGrid`, a duplicate prefab)
  causes its own mess. When in doubt, ask me to check what's already on
  GitHub before you start.
- **Pull before you start new Editor work, not after.** If you begin a
  session without fetching first, you risk building on a stale base — which
  is what causes the "GitHub Desktop won't let me push, there are newer
  commits on remote" dialog. Not harmful (it just means a merge is needed
  before pushing), but easy to avoid: **Fetch, then Pull, before opening
  Unity** at the start of a session.

## If you're ever unsure

Just ask me to check. I can always run `git fetch` and tell you exactly
what's on GitHub right now versus what you expect to see — that's a much
faster way to catch a gap than discovering it days later when something
looks "missing" in the Editor.
