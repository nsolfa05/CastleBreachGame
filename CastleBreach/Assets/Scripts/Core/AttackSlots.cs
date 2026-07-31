using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attack-slot reservation (crowd control at objectives). The force-based crowd
/// behaviors in MonsterAI (separation, give-way, yield) all react to crowding
/// AFTER it happens, and can only redistribute monsters into space that
/// actually exists nearby — which is why a target boxed into a tight alcove
/// still jams: there's nowhere to the side to spread into. This solves it from
/// the other end. Each target exposes the discrete standing positions ("slots")
/// from which a monster can actually hit it, derived live from the real
/// walkable tiles around it; a monster claims one, walks to it, holds it, and
/// releases it when done.
///
/// When every open slot is taken, overflow monsters don't just wait against a
/// wall: each target also exposes its "walled slots" — tiles that would be
/// valid standing positions but for a breakable wall currently on them. An
/// overflow monster claims the nearest of those and routes to it, which comes
/// back as "break this wall" (the wall IS the goal tile), so it breaks that
/// specific ring wall open and takes the fresh slot. Different overflow monsters
/// claim different ring tiles, so a horde widens the breach around a boxed-in
/// target instead of single-filing through one gap forever. Only tiles within
/// attack range of the target are ever offered, so this only ever eats the seal
/// ring immediately around the target — never far-off maze walls, which monsters
/// still route around normally.
///
/// Deliberately static (no scene object, so nothing to wire in the Editor).
/// State survives scene reloads, so PathGrid.Awake calls Reset() when the grid
/// is (re)built — on load and on R-to-restart — to drop claims against the
/// previous run's now-destroyed targets.
///
/// What a slot set depends on is captured by SlotProfile: attack range plus the
/// two movement flags. Any two monster types sharing a profile share one
/// generated candidate list (a plain Zombie and an Armored Zombie compute the
/// same tiles), while a future ranged or flying type gets its own — no per-type
/// wiring, it falls out of the key. Claims, though, are per TILE regardless of
/// profile, so a melee and a ranged monster can never end up standing on the
/// same tile.
///
/// Generation only ever does cheap local work (walkability + range + line of
/// sight over a small bounding box) and is cached against PathGrid.Version, so
/// it recomputes only when something is actually built or broken near the
/// target. Reachability is NOT checked here — that's left to the claiming
/// monster's own path solve, reusing machinery that already exists.
/// </summary>
public static class AttackSlots
{
    /// <summary>Nominal monster body radius used when deciding whether a tile is
    /// close enough to hit from. A circle collider of radius 0.5 at bodyScale 1;
    /// slightly under, so slots sit comfortably inside attack range rather than
    /// right on its edge where jitter could drop a monster out of range.</summary>
    private const float BodyRadius = 0.4f;

    /// <summary>The three things that decide which tiles are valid slots for a
    /// monster. Two monster types with equal values share one candidate list.</summary>
    private readonly struct SlotProfile : System.IEquatable<SlotProfile>
    {
        public readonly float AttackRange;
        public readonly bool PassesThroughGates;
        public readonly bool FliesOverBarriers;

        public SlotProfile(MonsterDefinition d)
        {
            AttackRange = d.attackRange;
            PassesThroughGates = d.passesThroughGates;
            FliesOverBarriers = d.fliesOverBarriers;
        }

        public bool Equals(SlotProfile o) =>
            AttackRange == o.AttackRange && PassesThroughGates == o.PassesThroughGates &&
            FliesOverBarriers == o.FliesOverBarriers;

        public override bool Equals(object o) => o is SlotProfile p && Equals(p);
        public override int GetHashCode() =>
            AttackRange.GetHashCode() ^ (PassesThroughGates ? 1 : 0) ^ (FliesOverBarriers ? 2 : 0);
    }

    private sealed class CachedCandidates
    {
        public int Version = -1;
        // Open = tiles a monster can stand on right now and hit from.
        // Walled = tiles that WOULD be valid slots but for a breakable wall
        // currently sitting on them; an overflow monster claims one of these and
        // breaks the wall to open a fresh standing position (see Candidates).
        public readonly List<Vector2Int> Open = new List<Vector2Int>();
        public readonly List<Vector2Int> Walled = new List<Vector2Int>();
    }

    private sealed class TargetSlots
    {
        // One occupant per tile, shared across profiles — two monsters never
        // stand on the same tile no matter how their ranges overlap.
        public readonly Dictionary<Vector2Int, MonsterAI> Claims = new Dictionary<Vector2Int, MonsterAI>();
        public readonly Dictionary<SlotProfile, CachedCandidates> ByProfile =
            new Dictionary<SlotProfile, CachedCandidates>();
    }

    private static readonly Dictionary<Transform, TargetSlots> ByTarget =
        new Dictionary<Transform, TargetSlots>();

    /// <summary>Wipe all slot state — called from PathGrid.Awake on (re)load.</summary>
    public static void Reset() => ByTarget.Clear();

    /// <summary>
    /// Claim the nearest free slot to <paramref name="fromWorld"/> around
    /// <paramref name="target"/>, marking it held by <paramref name="claimant"/>,
    /// and return its tile — or null if the target has no free slot right now
    /// (or none within <paramref name="maxDistance"/>, see below). The caller is
    /// expected to release any slot it already holds before calling.
    ///
    /// An OPEN slot (somewhere the monster can already stand) always wins. Only
    /// when every open slot is taken AND <paramref name="allowWalled"/> is set
    /// does it fall back to the nearest free WALLED slot — a would-be position
    /// sealed behind a breakable wall, which the claimant then breaks open (its
    /// route to that tile comes back MustBreak on that exact wall). That's the
    /// "widen the breach" path: with open slots exhausted, extra monsters each
    /// grab a distinct ring wall and open a new standing spot rather than piling
    /// up against the one gap. Claims are per tile and shared across the two
    /// lists, so a walled slot stays claimed by the same monster once its wall is
    /// broken and the tile becomes open.
    ///
    /// <paramref name="maxDistance"/> caps how far away a claim may be from
    /// <paramref name="fromWorld"/> — used by MonsterAI.TryMigrateForBlockedAlly
    /// to keep a migration a genuine LOCAL shuffle (step to the next open tile
    /// over) rather than a straight-line-nearest search that can reach clear
    /// across a crowded ring to whatever tile happens to be marginally closer,
    /// even if that means two monsters crossing paths (and shoving through each
    /// other) to swap places. Unbounded by default for the normal initial claim,
    /// where reaching across open ground to a legitimate far slot is fine.
    /// </summary>
    public static Vector2Int? ClaimNearestSlot(Transform target, MonsterDefinition definition,
                                               Vector2 fromWorld, MonsterAI claimant,
                                               Vector2Int? excludeTile = null, bool allowWalled = false,
                                               float maxDistance = float.PositiveInfinity)
    {
        if (target == null || definition == null) return null;

        var ts = SlotsFor(target);
        var cc = Candidates(ts, target, definition);

        Vector2Int? best = NearestFree(ts, cc.Open, fromWorld, claimant, excludeTile, maxDistance);
        if (!best.HasValue && allowWalled)
            best = NearestFree(ts, cc.Walled, fromWorld, claimant, excludeTile, maxDistance);

        if (!best.HasValue) return null;
        ts.Claims[best.Value] = claimant;
        return best;
    }

    /// <summary>Nearest free tile within maxDistance in one candidate list to fromWorld, or null.</summary>
    private static Vector2Int? NearestFree(TargetSlots ts, List<Vector2Int> tiles, Vector2 fromWorld,
                                           MonsterAI claimant, Vector2Int? excludeTile, float maxDistance)
    {
        float maxSqr = maxDistance * maxDistance; // PositiveInfinity squared is still PositiveInfinity — no overflow
        Vector2Int best = default;
        float bestSqr = float.MaxValue;
        bool found = false;
        foreach (var tile in tiles)
        {
            // Skip the caller's current tile when it's migrating (see
            // MonsterAI.TryMigrateForBlockedAlly) — it wants a DIFFERENT slot,
            // and its own tile would otherwise always win as the nearest.
            if (excludeTile.HasValue && tile == excludeTile.Value) continue;

            // A slot held by a still-living monster is taken; one whose holder
            // has been destroyed (== null) is free again — this self-heals any
            // claim a monster failed to release on death.
            if (ts.Claims.TryGetValue(tile, out var holder) && holder != null && holder != claimant)
                continue;

            float sqr = ((Vector2)GridMath.TileCenterWorld(tile) - fromWorld).sqrMagnitude;
            if (sqr > maxSqr) continue;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = tile;
                found = true;
            }
        }
        return found ? best : (Vector2Int?)null;
    }

    /// <summary>Release a slot if it's currently held by this claimant.</summary>
    public static void Release(Transform target, Vector2Int tile, MonsterAI claimant)
    {
        if (target == null) return;
        if (ByTarget.TryGetValue(target, out var ts) &&
            ts.Claims.TryGetValue(tile, out var holder) && holder == claimant)
            ts.Claims.Remove(tile);
    }

    /// <summary>
    /// Claim EXACTLY <paramref name="tile"/> — and only if it's a valid OPEN slot
    /// for this monster type right now and not already held by someone else living
    /// (already held by this claimant counts as success). Returns true on success.
    ///
    /// Unlike ClaimNearestSlot this never searches or substitutes a different
    /// tile: it's the "just take the spot I'm literally standing on" primitive.
    /// A monster that has physically drifted onto a perfectly good free slot
    /// should settle onto THAT rather than shove back across the crowd to reach
    /// whatever tile it reserved earlier — claim where you stand. That single
    /// rule removes most of the crossing/pushing, because a monster then almost
    /// never needs to walk THROUGH other monsters to reach its slot; it adopts
    /// wherever it already is. OPEN slots only (walkable now) — you can't be
    /// standing on a walled one.
    /// </summary>
    public static bool TryClaimTile(Transform target, MonsterDefinition definition, Vector2Int tile, MonsterAI claimant)
    {
        if (target == null || definition == null) return false;
        var ts = SlotsFor(target);
        if (!Candidates(ts, target, definition).Open.Contains(tile)) return false;
        if (ts.Claims.TryGetValue(tile, out var holder) && holder != null && holder != claimant) return false;

        ts.Claims[tile] = claimant;
        return true;
    }

    /// <summary>
    /// True if <paramref name="tile"/> is still both a valid candidate slot for
    /// this monster type (as of the current grid) AND still claimed by this
    /// claimant. A monster whose slot goes invalid — a wall built on it, say —
    /// gets false here and should release + reclaim.
    /// </summary>
    public static bool IsValidClaim(Transform target, Vector2Int tile, MonsterDefinition definition, MonsterAI claimant)
    {
        if (target == null || definition == null) return false;
        if (!ByTarget.TryGetValue(target, out var ts)) return false;
        if (!ts.Claims.TryGetValue(tile, out var holder) || holder != claimant) return false;

        // Valid whether it's a standing slot or one still being broken open — a
        // walled slot the monster is opening must not read as invalid mid-break.
        var cc = Candidates(ts, target, definition);
        return cc.Open.Contains(tile) || cc.Walled.Contains(tile);
    }

    /// <summary>
    /// True if <paramref name="tile"/> is currently a WALLED slot for this target
    /// — a would-be standing position still sealed behind a breakable wall. A
    /// monster holding such a slot is deliberately breaking in to it, so callers
    /// must not mistake the resulting "must break a wall to reach my slot" routing
    /// for the slot being genuinely unreachable and drop the claim (see
    /// MonsterAI.UpdateSlot). Goes false the instant the wall is broken and the
    /// tile becomes an open slot the monster then stands on.
    /// </summary>
    public static bool SlotIsCurrentlyWalled(Transform target, Vector2Int tile, MonsterDefinition definition)
    {
        if (target == null || definition == null) return false;
        if (!ByTarget.TryGetValue(target, out var ts)) return false;
        return Candidates(ts, target, definition).Walled.Contains(tile);
    }

    /// <summary>
    /// Scene-view diagnostic: draw every known slot — open slots as a solid cube
    /// (green free / red claimed), walled would-be slots as a wire cube (amber
    /// free / magenta claimed-and-being-broken-open) — so you can see the ring a
    /// target actually offers, which walls a horde is about to break for more
    /// room, and confirm a boxed-in target correctly has fewer open slots. Call
    /// only from an OnDrawGizmos context (PathGrid does, behind its own toggle).
    /// Iterates whatever candidate lists have been generated so far; a target no
    /// monster has approached yet simply hasn't generated any and draws nothing.
    /// </summary>
    public static void DebugDraw()
    {
        foreach (var pair in ByTarget)
        {
            if (pair.Key == null) continue;
            var ts = pair.Value;
            foreach (var byProfile in ts.ByProfile.Values)
            {
                foreach (var tile in byProfile.Open)
                {
                    bool taken = ts.Claims.TryGetValue(tile, out var holder) && holder != null;
                    Gizmos.color = taken ? new Color(1f, 0.3f, 0.3f, 0.6f) : new Color(0.3f, 1f, 0.4f, 0.5f);
                    Gizmos.DrawCube(GridMath.TileCenterWorld(tile), Vector3.one * 0.35f);
                }
                // Walled would-be slots (a wall waiting to be broken open into a
                // slot): amber if free, magenta if a monster has claimed it and
                // is on its way to break it.
                foreach (var tile in byProfile.Walled)
                {
                    bool taken = ts.Claims.TryGetValue(tile, out var holder) && holder != null;
                    Gizmos.color = taken ? new Color(1f, 0.2f, 0.8f, 0.6f) : new Color(1f, 0.6f, 0.1f, 0.45f);
                    Gizmos.DrawWireCube(GridMath.TileCenterWorld(tile), Vector3.one * 0.5f);
                }
            }
        }
    }

    private static TargetSlots SlotsFor(Transform target)
    {
        if (!ByTarget.TryGetValue(target, out var ts))
        {
            ts = new TargetSlots();
            ByTarget[target] = ts;
        }
        return ts;
    }

    /// <summary>
    /// The candidate slots for one profile around one target — both the open
    /// tiles a monster can stand on and the walled tiles it could break open into
    /// slots — regenerated only when the grid has changed since last computed.
    /// </summary>
    private static CachedCandidates Candidates(TargetSlots ts, Transform target, MonsterDefinition definition)
    {
        var profile = new SlotProfile(definition);
        if (!ts.ByProfile.TryGetValue(profile, out var cached))
        {
            cached = new CachedCandidates();
            ts.ByProfile[profile] = cached;
        }

        var grid = PathGrid.Instance;
        if (grid == null) { cached.Open.Clear(); cached.Walled.Clear(); return cached; }
        if (cached.Version == grid.Version) return cached;

        cached.Version = grid.Version;
        cached.Open.Clear();
        cached.Walled.Clear();

        var targetCollider = target.GetComponentInParent<Collider2D>();
        if (targetCollider == null) return cached;

        // A tile is a valid slot if a monster standing at its center is close
        // enough to land a hit. Standing at the center, edge-to-edge distance to
        // the target ~= (center-to-surface) - BodyRadius, so the reach limit on
        // center-to-surface is attackRange + BodyRadius.
        float reach = definition.attackRange + BodyRadius;

        // Scan only the tiles whose centers could possibly be within reach of
        // the target's footprint — its bounds grown by reach (+ a half-tile so
        // the floor/ceil can't clip an edge tile).
        Bounds b = targetCollider.bounds;
        float pad = reach + 0.5f;
        int colMin = Mathf.FloorToInt(b.min.x - pad);
        int colMax = Mathf.FloorToInt(b.max.x + pad);
        // World Y grows upward but doc rows grow downward, so max world Y maps to
        // the smallest row index. Convert both extremes and let min/max sort it.
        int rowA = GridMath.WorldToTile(new Vector2(0f, b.min.y - pad)).y;
        int rowB = GridMath.WorldToTile(new Vector2(0f, b.max.y + pad)).y;
        int rowMin = Mathf.Min(rowA, rowB);
        int rowMax = Mathf.Max(rowA, rowB);

        for (int col = colMin; col <= colMax; col++)
        {
            for (int row = rowMin; row <= rowMax; row++)
            {
                var tile = new Vector2Int(col, row);
                if (!GridMath.InBounds(tile)) continue;

                bool standable = grid.IsStandable(tile, definition);
                // A tile is only ever interesting if a monster could hit the
                // target from it: either it's open ground now, or a breakable
                // wall sits on it that, once gone, would leave open ground. Skip
                // permanent terrain, the target's own tiles, and anything else.
                bool walled = !standable && grid.IsBreakableBarrier(tile, definition);
                if (!standable && !walled) continue;

                Vector2 center = GridMath.TileCenterWorld(tile);
                Vector2 surface = targetCollider.ClosestPoint(center);
                if ((center - surface).sqrMagnitude > reach * reach) continue; // out of attack range from here

                if (standable)
                {
                    // Ranged line of sight: an open slot is only valid if the shot
                    // to the target isn't itself blocked by a wall. For a melee
                    // monster standing right against the target this is trivially
                    // clear, so it costs nothing there; for a future ranged type
                    // it stops slots being generated on the far side of a wall it
                    // can't shoot through. Reuses the clearance check movement uses.
                    if (!grid.HasClearLine(center, surface, definition, target)) continue;
                    cached.Open.Add(tile);
                }
                else
                {
                    // Walled would-be slot: a wall on this tile blocks the line of
                    // sight FROM the tile by definition, so the clear-line test
                    // above is meaningless until the wall is gone — skip it. Range
                    // is enough to mark it as a spot worth breaking open; the
                    // reachability of the break itself is left to the claiming
                    // monster's own path solve, as with everything else here.
                    cached.Walled.Add(tile);
                }
            }
        }

        return cached;
    }
}
