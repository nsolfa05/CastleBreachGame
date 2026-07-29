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
/// releases it when done. Overflow monsters find every slot taken and simply
/// wait — deterministically, instead of grinding against space that isn't there.
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
        public readonly List<Vector2Int> Tiles = new List<Vector2Int>();
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
    /// (every one taken, or none exist because it's too boxed in). The caller is
    /// expected to release any slot it already holds before calling this.
    /// </summary>
    public static Vector2Int? ClaimNearestSlot(Transform target, MonsterDefinition definition,
                                               Vector2 fromWorld, MonsterAI claimant)
    {
        if (target == null || definition == null) return null;

        var ts = SlotsFor(target);
        var candidates = Candidates(ts, target, definition);

        Vector2Int best = default;
        float bestSqr = float.MaxValue;
        bool found = false;
        foreach (var tile in candidates)
        {
            // A slot held by a still-living monster is taken; one whose holder
            // has been destroyed (== null) is free again — this self-heals any
            // claim a monster failed to release on death.
            if (ts.Claims.TryGetValue(tile, out var holder) && holder != null && holder != claimant)
                continue;

            float sqr = ((Vector2)GridMath.TileCenterWorld(tile) - fromWorld).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = tile;
                found = true;
            }
        }

        if (!found) return null;
        ts.Claims[best] = claimant;
        return best;
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

        return Candidates(ts, target, definition).Contains(tile);
    }

    /// <summary>
    /// Scene-view diagnostic: draw every known slot as a small cube — green if
    /// free, red if claimed — so you can see the ring a target actually offers
    /// and confirm a boxed-in target correctly has fewer slots. Call only from
    /// an OnDrawGizmos context (PathGrid does, behind its own toggle). Iterates
    /// whatever candidate lists have been generated so far; a target no monster
    /// has approached yet simply hasn't generated any and draws nothing.
    /// </summary>
    public static void DebugDraw()
    {
        foreach (var pair in ByTarget)
        {
            if (pair.Key == null) continue;
            var ts = pair.Value;
            foreach (var byProfile in ts.ByProfile.Values)
            {
                foreach (var tile in byProfile.Tiles)
                {
                    bool taken = ts.Claims.TryGetValue(tile, out var holder) && holder != null;
                    Gizmos.color = taken ? new Color(1f, 0.3f, 0.3f, 0.6f) : new Color(0.3f, 1f, 0.4f, 0.5f);
                    Gizmos.DrawCube(GridMath.TileCenterWorld(tile), Vector3.one * 0.35f);
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
    /// The candidate slot tiles for one profile around one target, regenerated
    /// only when the grid has changed since they were last computed.
    /// </summary>
    private static List<Vector2Int> Candidates(TargetSlots ts, Transform target, MonsterDefinition definition)
    {
        var profile = new SlotProfile(definition);
        if (!ts.ByProfile.TryGetValue(profile, out var cached))
        {
            cached = new CachedCandidates();
            ts.ByProfile[profile] = cached;
        }

        var grid = PathGrid.Instance;
        if (grid == null) { cached.Tiles.Clear(); return cached.Tiles; }
        if (cached.Version == grid.Version) return cached.Tiles;

        cached.Version = grid.Version;
        cached.Tiles.Clear();

        var targetCollider = target.GetComponentInParent<Collider2D>();
        if (targetCollider == null) return cached.Tiles;

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
                if (!grid.IsStandable(tile, definition)) continue; // solid ground / the target itself / a wall → not a slot

                Vector2 center = GridMath.TileCenterWorld(tile);
                Vector2 surface = targetCollider.ClosestPoint(center);
                if ((center - surface).sqrMagnitude > reach * reach) continue; // out of attack range from here

                // Ranged line of sight: a slot is only valid if the shot to the
                // target isn't itself blocked by a wall. For a melee monster
                // standing right against the target this is trivially clear, so
                // it costs nothing there; for a future ranged type it stops
                // slots being generated on the far side of a wall it can't shoot
                // through. Reuses the same clearance check movement already uses.
                if (!grid.HasClearLine(center, surface, definition, target)) continue;

                cached.Tiles.Add(tile);
            }
        }

        return cached.Tiles;
    }
}
