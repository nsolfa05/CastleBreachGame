using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Grid routing service (design doc §6) — the single place that knows which
/// tiles are walkable and how to get from A to B around player-built mazes.
///
/// Two kinds of obstacle, deliberately kept separate:
/// - PERMANENT terrain: the castle's own border walls, painted into the wall
///   Tilemap by CastleMapGenerator. Never breakable, never removable.
/// - DYNAMIC structures: anything with a collider on the blocking layers —
///   player-built Walls and Gates, towers, the King. Found by a physics scan
///   rather than by requiring every prefab to register itself, so a new
///   structure type automatically blocks correctly with no extra wiring (and
///   the routing grid can never silently disagree with actual physics).
///
/// Monsters are deliberately NOT in the blocking layers: routing only ever
/// steers around static obstacles. Monster-vs-monster jostling stays with
/// physics collision + MonsterAI's own steer-around-neighbors nudge, so the
/// crowding behavior is unchanged by any of this.
///
/// Searching is BFS rather than A*: the grid is tiny (40x30 = 1200 cells), and
/// one BFS gives the whole reachable set in the same pass — which is exactly
/// what the "every route is sealed, what do I break?" fallback needs. If maps
/// ever get big enough for that to matter, swapping in A* for the reached case
/// is a contained change to Solve().
/// </summary>
public class PathGrid : MonoBehaviour
{
    public static PathGrid Instance { get; private set; }

    [Header("Static terrain")]
    [Tooltip("The castle's own border wall Tilemap — permanent terrain that can never be broken. Drag the Wall tilemap here.")]
    [SerializeField] private Tilemap wallTilemap;

    [Header("Dynamic obstacles")]
    [Tooltip("Layers whose colliders block movement — set to Structure and King. Do NOT include Enemy: monsters crowd past each other physically, they never route around one another.")]
    [SerializeField] private LayerMask blockingLayers;

    [Header("Cost control")]
    [Tooltip("How many monsters may start a new route search in one frame. Any extras keep following their current route and try again next frame, so a big wall collapse can't spike the frame time.")]
    [SerializeField] private int searchesPerFrame = 8;

    [Tooltip("Seconds between rescans of the obstacle grid. A rescan only makes monsters re-route if something actually changed, so this stays cheap even when nothing is being built.")]
    [SerializeField] private float rescanInterval = 0.25f;

    [Header("Debug")]
    [Tooltip("Draw blocked tiles in the Scene view while this object is selected — red = permanent terrain, orange = breakable structure, blue = gate.")]
    [SerializeField] private bool drawBlockedTiles = false;

    /// <summary>What a search concluded about getting to the requested goal.</summary>
    public enum PathOutcome
    {
        /// <summary>A route exists; the path list is filled in.</summary>
        PathFound,
        /// <summary>Every route is sealed. The path leads to the sealing obstacle, which must be broken.</summary>
        MustBreak,
        /// <summary>Sealed in with nothing breakable reachable (shouldn't normally happen).</summary>
        NoRoute,
    }

    private struct Cell
    {
        public bool permanent;      // castle border wall — unbreakable terrain
        public Transform blocker;   // structure occupying this tile (null = clear)
        public bool isBarrier;      // that structure is a player-built Wall/Gate
        public bool isGate;         // ...and specifically a Gate

        public bool Matches(in Cell other) =>
            permanent == other.permanent && blocker == other.blocker &&
            isBarrier == other.isBarrier && isGate == other.isGate;
    }

    // 'cells' is what searches read. 'scratch' is rebuilt from scratch on every
    // rescan and only swapped in afterwards, so a rescan can compare the two and
    // tell whether anything genuinely moved — see Rescan.
    private Cell[,] cells;
    private Cell[,] scratch;

    // Search scratch, reused between searches so routing allocates nothing.
    // visitStamp holds the id of the search that last touched a cell, which
    // avoids having to clear 1200 entries before every single search.
    private int[,] visitStamp;
    private int[,] distance;
    private Vector2Int[,] parent;
    private int searchId;
    private readonly Queue<Vector2Int> queue = new Queue<Vector2Int>();

    private float nextRescanTime;
    private int searchesLeftThisFrame;

    /// <summary>
    /// Bumped only when the obstacle layout actually changes. Monsters compare
    /// against it to know their cached route may be stale — a rescan that finds
    /// nothing new deliberately does NOT bump it, so nobody re-routes for free.
    /// </summary>
    public int Version { get; private set; }

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int( 1,  0), new Vector2Int(-1,  0),
        new Vector2Int( 0,  1), new Vector2Int( 0, -1),
        new Vector2Int( 1,  1), new Vector2Int( 1, -1),
        new Vector2Int(-1,  1), new Vector2Int(-1, -1),
    };

    private void Awake()
    {
        Instance = this;
        cells = new Cell[GridMath.Columns, GridMath.Rows];
        scratch = new Cell[GridMath.Columns, GridMath.Rows];
        visitStamp = new int[GridMath.Columns, GridMath.Rows];
        distance = new int[GridMath.Columns, GridMath.Rows];
        parent = new Vector2Int[GridMath.Columns, GridMath.Rows];
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        searchesLeftThisFrame = searchesPerFrame;

        if (Time.time >= nextRescanTime)
        {
            nextRescanTime = Time.time + rescanInterval;
            Rescan();
        }
    }

    /// <summary>Refresh the obstacle grid on the next frame — call right after building or breaking something so monsters react immediately instead of on the next routine rescan.</summary>
    public void MarkDirty() => nextRescanTime = 0f;

    /// <summary>
    /// Budget gate: monsters ask before starting a search so a mass re-route
    /// (a long wall segment falling) spreads over several frames instead of
    /// landing in one.
    /// </summary>
    public bool TryBeginSearch()
    {
        if (searchesLeftThisFrame <= 0) return false;
        searchesLeftThisFrame--;
        return true;
    }

    /// <summary>
    /// Rebuilds the obstacle grid from the wall Tilemap plus a physics scan of
    /// the blocking layers, into 'scratch', then compares against the live grid
    /// before swapping. That comparison is the whole point: without it every
    /// rescan would look like a change and force every monster to re-route
    /// several times a second for nothing.
    /// </summary>
    private void Rescan()
    {
        // Terrain. Recomputed every pass rather than cached once, so this can't
        // depend on whether CastleMapGenerator has painted the map yet (Start
        // order between the two is not guaranteed) and stays correct if a map
        // is ever regenerated mid-game.
        for (int col = 0; col < GridMath.Columns; col++)
        {
            for (int row = 0; row < GridMath.Rows; row++)
            {
                scratch[col, row] = new Cell
                {
                    permanent = wallTilemap != null &&
                                wallTilemap.HasTile(GridMath.TileToCell(new Vector2Int(col, row)))
                };
            }
        }

        var hits = Physics2D.OverlapAreaAll(Vector2.zero,
            new Vector2(GridMath.Columns, GridMath.Rows), blockingLayers);

        foreach (var hit in hits)
        {
            // Anchor on whatever owns the Health component so every tile of a
            // multi-tile structure reports the same Transform — that identity
            // is what lets a monster recognise "this is the thing I'm heading
            // for" and "this is the single object I need to break".
            var health = hit.GetComponentInParent<Health>();
            Transform root = health != null ? health.transform : hit.transform;
            var barrier = hit.GetComponentInParent<Barrier>();

            Bounds bounds = hit.bounds;
            const float inset = 0.05f; // sample inside the collider, not on its seam
            int colMin = Mathf.FloorToInt(bounds.min.x + inset);
            int colMax = Mathf.FloorToInt(bounds.max.x - inset);
            int rowBottomMin = Mathf.FloorToInt(bounds.min.y + inset);
            int rowBottomMax = Mathf.FloorToInt(bounds.max.y - inset);

            for (int col = colMin; col <= colMax; col++)
            {
                for (int rowBottom = rowBottomMin; rowBottom <= rowBottomMax; rowBottom++)
                {
                    var tile = new Vector2Int(col, GridMath.Rows - 1 - rowBottom);
                    if (!GridMath.InBounds(tile)) continue;

                    ref Cell cell = ref scratch[tile.x, tile.y];
                    cell.blocker = root;
                    cell.isBarrier = barrier != null;
                    cell.isGate = barrier != null && barrier.IsGate;
                }
            }
        }

        bool changed = false;
        for (int col = 0; col < GridMath.Columns && !changed; col++)
            for (int row = 0; row < GridMath.Rows && !changed; row++)
                if (!scratch[col, row].Matches(cells[col, row])) changed = true;

        (cells, scratch) = (scratch, cells);
        if (changed) Version++;
    }

    /// <summary>
    /// Can this monster type stand on this tile? goalRoot is treated as walkable
    /// so a search can actually arrive at the structure/King it is heading for —
    /// otherwise every goal would be unreachable by virtue of being solid.
    /// </summary>
    private bool IsWalkable(Vector2Int tile, MonsterDefinition definition, Transform goalRoot)
    {
        if (!GridMath.InBounds(tile)) return false;
        ref Cell cell = ref cells[tile.x, tile.y];

        if (cell.permanent) return false;          // castle border — nothing walks through it
        if (cell.blocker == null) return true;
        if (cell.blocker == goalRoot) return true; // the target never blocks the route to itself

        if (cell.isGate && definition.passesThroughGates) return true;
        if (cell.isBarrier && definition.fliesOverBarriers) return true;

        return false;
    }

    /// <summary>Diagonal steps may not cut a corner between two blocked tiles.</summary>
    private bool CanStepDiagonally(Vector2Int from, Vector2Int step, MonsterDefinition definition, Transform goalRoot)
    {
        if (step.x == 0 || step.y == 0) return true;
        return IsWalkable(new Vector2Int(from.x + step.x, from.y), definition, goalRoot) &&
               IsWalkable(new Vector2Int(from.x, from.y + step.y), definition, goalRoot);
    }

    private static Transform RootOf(Transform target)
    {
        if (target == null) return null;
        var health = target.GetComponentInParent<Health>();
        return health != null ? health.transform : target;
    }

    /// <summary>
    /// Routes from start toward goal for one monster type.
    ///
    /// When the goal is reachable this fills outPath and returns PathFound.
    /// When it is NOT, the same search already knows every tile the monster can
    /// currently reach, so it picks the nearest breakable obstacle sitting on
    /// the BOUNDARY of that reachable region — meaning breaking it always opens
    /// ground the monster couldn't otherwise get to — fills outPath with the
    /// route to it, and returns MustBreak.
    ///
    /// That ordering is what keeps "walk the maze" and "smash through" from
    /// being confused with each other: while any route to the goal exists,
    /// PathFound wins and nothing is ever attacked merely for standing nearby.
    /// Obstacles only become targets once there is genuinely no way around,
    /// which is the design doc's "breakable if no path around it" (§6).
    ///
    /// Nearest-on-the-boundary is deliberately the simple rule. The smarter
    /// version — simulate removing each boundary candidate and keep whichever
    /// most shortens the resulting route — would slot in exactly where
    /// bestBreak is chosen below, without disturbing anything else.
    /// </summary>
    public PathOutcome Solve(Vector2Int start, Vector2Int goal, MonsterDefinition definition,
                             Transform goalObject, List<Vector2Int> outPath, out Transform blocker)
    {
        outPath.Clear();
        blocker = null;
        if (cells == null || definition == null) return PathOutcome.NoRoute;
        if (!GridMath.InBounds(start) || !GridMath.InBounds(goal)) return PathOutcome.NoRoute;

        Transform goalRoot = RootOf(goalObject);

        searchId++;
        queue.Clear();
        visitStamp[start.x, start.y] = searchId;
        distance[start.x, start.y] = 0;
        parent[start.x, start.y] = start;
        queue.Enqueue(start);

        bool reachedGoal = start == goal;

        // Best obstacle to break, gathered as we go. BFS dequeues in
        // nondecreasing distance order, so the first breakable obstacle we
        // bump into is already the nearest one — no second sweep of the grid
        // needed, and each blocked tile is only inspected once.
        var bestBreakFrom = start;
        Transform bestBreak = null;
        int bestDistance = int.MaxValue;
        float bestGoalProximity = float.MaxValue;

        while (queue.Count > 0 && !reachedGoal)
        {
            var current = queue.Dequeue();
            int currentDistance = distance[current.x, current.y];

            foreach (var step in Directions)
            {
                var next = new Vector2Int(current.x + step.x, current.y + step.y);
                if (!GridMath.InBounds(next)) continue;
                if (visitStamp[next.x, next.y] == searchId) continue;

                if (!IsWalkable(next, definition, goalRoot))
                {
                    // Blocked, and never enqueued — stamp it so it's only
                    // considered as a break candidate once.
                    visitStamp[next.x, next.y] = searchId;
                    if (currentDistance <= bestDistance)
                    {
                        ref Cell cell = ref cells[next.x, next.y];
                        // Terrain can't be broken; the goal isn't an obstacle;
                        // and only things with Health can be destroyed at all.
                        if (!cell.permanent && cell.blocker != null && cell.blocker != goalRoot)
                        {
                            var health = cell.blocker.GetComponent<Health>();
                            if (health != null && !health.IsDead)
                            {
                                float goalProximity = (next - goal).sqrMagnitude;
                                if (currentDistance < bestDistance ||
                                    goalProximity < bestGoalProximity)
                                {
                                    bestDistance = currentDistance;
                                    bestGoalProximity = goalProximity;
                                    bestBreak = cell.blocker;
                                    bestBreakFrom = current;
                                }
                            }
                        }
                    }
                    continue;
                }

                if (!CanStepDiagonally(current, step, definition, goalRoot)) continue;

                visitStamp[next.x, next.y] = searchId;
                distance[next.x, next.y] = currentDistance + 1;
                parent[next.x, next.y] = current;

                if (next == goal) { reachedGoal = true; break; }
                queue.Enqueue(next);
            }
        }

        if (reachedGoal)
        {
            BuildPath(start, goal, outPath);
            return PathOutcome.PathFound;
        }

        if (bestBreak == null) return PathOutcome.NoRoute;

        blocker = bestBreak;
        BuildPath(start, bestBreakFrom, outPath);
        return PathOutcome.MustBreak;
    }

    private void BuildPath(Vector2Int start, Vector2Int end, List<Vector2Int> outPath)
    {
        var step = end;
        while (step != start)
        {
            outPath.Add(step);
            step = parent[step.x, step.y];
        }
        outPath.Reverse();
    }

    /// <summary>
    /// True if a monster of this type could walk the straight line between two
    /// world points without clipping an obstacle. Used to skip the route
    /// entirely when the target is in plain sight — which is what keeps
    /// movement on open ground looking exactly as it did before pathfinding
    /// existed, rather than visibly stepping tile to tile.
    /// </summary>
    public bool HasClearLine(Vector2 from, Vector2 to, MonsterDefinition definition, Transform goalObject)
    {
        if (cells == null || definition == null) return true;

        Transform goalRoot = RootOf(goalObject);
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.001f) return true;

        // Checked at three offsets across the line, not just its centerline —
        // a monster has real width, so a line whose bare tile-centers are all
        // open can still clip a wall corner the moment the monster's own body
        // sweeps along it. Missing this let a monster sitting right next to a
        // corner keep concluding "clear line, go straight" every frame and
        // walking straight back into the wall it was already touching,
        // instead of falling through to the actual planned route below, which
        // already correctly routes around it.
        Vector2 direction = delta / length;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float clearance = Mathf.Max(0.1f, definition.bodyScale * 0.5f);

        const float stepSize = 0.4f;
        int steps = Mathf.CeilToInt(length / stepSize);
        for (int i = 1; i <= steps; i++)
        {
            Vector2 point = from + delta * (i / (float)steps);
            if (!IsWalkable(GridMath.WorldToTile(point), definition, goalRoot)) return false;
            if (!IsWalkable(GridMath.WorldToTile(point + perpendicular * clearance), definition, goalRoot)) return false;
            if (!IsWalkable(GridMath.WorldToTile(point - perpendicular * clearance), definition, goalRoot)) return false;
        }
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawBlockedTiles || cells == null) return;

        for (int col = 0; col < GridMath.Columns; col++)
        {
            for (int row = 0; row < GridMath.Rows; row++)
            {
                ref Cell cell = ref cells[col, row];
                if (!cell.permanent && cell.blocker == null) continue;

                Gizmos.color = cell.permanent ? new Color(1f, 0.2f, 0.2f, 0.35f)
                             : cell.isGate ? new Color(0.3f, 0.6f, 1f, 0.45f)
                             : new Color(1f, 0.65f, 0.15f, 0.45f);
                Gizmos.DrawCube(GridMath.TileCenterWorld(new Vector2Int(col, row)), Vector3.one * 0.9f);
            }
        }
    }
}
