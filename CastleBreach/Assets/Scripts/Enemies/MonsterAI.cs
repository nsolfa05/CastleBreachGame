using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one AI script shared by EVERY monster type — all stats and behavior
/// flags come from the assigned MonsterDefinition asset (§7.3), so the same
/// generic Monster prefab plays as a Zombie, Armored Zombie, Skeleton,
/// Goblin, or Cyclops depending on which definition it's given.
///
/// Core behavior: walk straight toward the King; chase the player instead
/// when they come within the definition's target range; attack whatever is
/// in reach, including structures blocking the path. Specials handled here:
/// - targetsOnlyKing / praiseTowerLureRange (Goblin)
/// - kingPriorityRange: beats chasing the player if the King is this close
///   (any monster, not just one type — see ChooseTarget)
/// - Structure Priority Range / Structure Interest Range / Structure Near
///   King Range (Cyclops; also usable on any monster) — see ChooseTarget
/// - extraLives + invulnerable bone-pile revive (Skeleton)
///
/// MOVEMENT: routes around player-built mazes via PathGrid (§6) — see
/// ResolveNavigation and SteeringPoint. With nothing in the way the result is
/// identical to the straight-line movement this used before, and crowding
/// between monsters is still pure physics + SteerAroundNeighbors; routing only
/// ever accounts for static obstacles, never for other monsters.
///
/// NOTE: the targeting RANGES below (attack range, the structure priority /
/// interest / near-King ranges) still measure straight-line distance via
/// DistanceBetween, not route length. That is deliberate — they are all
/// "how far away is this" questions, they are called many times per frame,
/// and their current values were tuned against straight-line distance.
/// Switching them to route length is a real behavior change to make on
/// purpose, not a side effect of pathfinding existing; see ROADMAP.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class MonsterAI : MonoBehaviour
{
    [Tooltip("Which monster this is — every stat reads from this asset. The WaveSpawner overrides this per spawn group; the value set on the prefab is just the default.")]
    [SerializeField] private MonsterDefinition definition;

    [Header("Scene wiring (identical for every monster type)")]
    [SerializeField] private CurrencyDrop currencyDropPrefab;

    [Tooltip("Layers holding player-built structures — set to the Structure layer.")]
    [SerializeField] private LayerMask structureLayers;

    [Tooltip("The tinted body renderer. Leave empty to use the SpriteRenderer on this object.")]
    [SerializeField] private SpriteRenderer body;

    [Header("Crowd avoidance (shared behavior, not per-monster-type data)")]
    [Tooltip("How far ahead to check for another monster directly blocking the path, in tiles.")]
    [SerializeField] private float avoidanceLookAhead = 0.6f;

    [Tooltip("Width of the ahead-check — roughly your own body's radius.")]
    [SerializeField] private float avoidanceProbeRadius = 0.3f;

    [Tooltip("Actual physics velocity below this (units/sec) counts as \"stuck\" — used to detect a monster that's committed to attacking a structure but is physically blocked (e.g. trapped behind another monster) from ever reaching it.")]
    [SerializeField] private float stuckVelocityThreshold = 0.15f;

    [Header("Routing around walls (shared behavior, not per-monster-type data)")]
    [Tooltip("Seconds between route recalculations. A route is also recalculated immediately whenever the target changes or something is built/destroyed, so this only paces the routine refresh that tracks a moving target.")]
    [SerializeField] private float repathInterval = 0.5f;

    [Tooltip("How close (tiles) a monster must get to a route waypoint before switching to aim at the next one. Smaller = hugs the intended tile-by-tile path more closely, which matters most at corners: too generous a radius lets a monster start swinging toward the next leg of the route well before it's actually rounded the corner, cutting across the inside edge of the wall forming it. Too small makes it harder to ever count a waypoint as \"reached\" while jostled in a crowd, which can stall progress instead. 0.25 keeps a monster within about a quarter-tile of the intended line.")]
    [SerializeField] private float waypointArrivalRadius = 0.25f;

    /// <summary>Fired exactly once, when the monster is permanently dead
    /// (a Skeleton's first death does NOT fire this — it revives).</summary>
    public event Action<MonsterAI> Killed;

    public MonsterDefinition Definition => definition;

    private enum TelegraphPhase { Idle, Winding, Cooldown }

    private Rigidbody2D rb;
    private Health health;
    private TelegraphedAreaAttack telegraph;
    private float nextAttackTime;
    private int livesRemaining;
    private bool bonePileActive;
    private Vector3 activeScale;
    private float avoidSide; // -1 or +1, fixed per instance so avoidance doesn't flicker sides

    // Crowd-detection mask covers BOTH monster layers, not just this instance's
    // own one. A monster with Passes Through Gates gets moved onto the
    // GatePasser layer at spawn (see Start) so Gate's collider can tell it
    // apart from ordinary monsters — but that's purely a Gate-collision trick,
    // not a "these are different kinds of crowd" distinction, so
    // SteerAroundNeighbors still needs to see every monster regardless of
    // which of the two layers it landed on. Static: computed once, shared by
    // every instance, not per-monster state.
    private static readonly int EnemyCrowdMask = LayerMask.GetMask("Enemy", "GatePasser");
    private float lastAttackedPlayerTime = float.NegativeInfinity;
    private Transform committedStructureTarget; // non-null while already committed to attacking a specific structure
    private TelegraphPhase telegraphPhase = TelegraphPhase.Idle;
    private float telegraphPhaseEndTime;
    private Vector2 lockedBoxCenter;
    private float currentSpeedScale = 1f; // telegraph-attack movement ramp (1 = full speed, 0 = fully stopped)

    // Routing state (Phase 4). The path is a list of tiles to walk; blockedByStructure
    // is set when every route to the real target is sealed and something has to be
    // broken to get through.
    private readonly List<Vector2Int> path = new List<Vector2Int>();
    private int pathIndex;
    private int pathGridVersion = -1;
    private Transform pathTarget;
    private Vector2Int pathGoalTile;
    private float nextRepathTime;
    private Transform blockedByStructure;

    /// <summary>Called by the WaveSpawner right after Instantiate, before the first frame.</summary>
    public void SetDefinition(MonsterDefinition newDefinition) => definition = newDefinition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        telegraph = GetComponent<TelegraphedAreaAttack>();
        health.Died += OnDied;
        if (body == null) body = GetComponent<SpriteRenderer>();
        avoidSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
    }

    /// <summary>
    /// Gap between this monster's collider and the target's collider — NOT
    /// center-to-center distance. A big target (the King at 1.8x scale, or
    /// any 2x2 tower) physically stops a monster's collider well outside 1
    /// world unit from its CENTER, so comparing center-to-center against a
    /// ~1-tile Attack Range meant monsters could walk up, get physically
    /// stopped by collision, and STILL never register as "in range" —
    /// visibly "attacking" while dealing zero damage, forever. Measuring to
    /// the collider surface instead makes Attack Range mean the same thing
    /// regardless of how big the target is.
    /// </summary>
    private float DistanceToTarget(Transform target) => DistanceBetween(transform, target);

    /// <summary>
    /// Edge-to-edge distance between any two colliders (falls back to raw
    /// transform distance if either side lacks a Collider2D). This is the ONE
    /// place every distance-based targeting decision measures "how far apart
    /// are these two things", and it is straight-line by design.
    ///
    /// Routing (PathGrid) deliberately did NOT take this over. Doing so would
    /// mean a graph search behind every attack-range and priority-range check,
    /// many times per frame per monster, and would silently retune every
    /// existing range value from "as the crow flies" to "as the monster walks".
    /// The one rule with a real appetite for route length is the King-progress
    /// guard inside Structure Interest Range (a structure the far side of a
    /// wall can read as closer-to-the-King than it is to walk to). Upgrading
    /// just that one is cheap — a shared breadth-first sweep out from the King
    /// per movement class, refreshed only when PathGrid.Version changes, gives
    /// route distance for every tile at once — and is tracked in ROADMAP as its
    /// own deliberate change rather than folded in here.
    /// </summary>
    private static float DistanceBetween(Transform a, Transform b)
    {
        var colliderA = a.GetComponentInParent<Collider2D>();
        var colliderB = b.GetComponentInParent<Collider2D>();
        if (colliderA != null && colliderB != null)
            return colliderA.Distance(colliderB).distance;
        return Vector2.Distance(a.position, b.position);
    }

    private void Start()
    {
        if (definition == null)
        {
            Debug.LogError($"MonsterAI on '{name}': no MonsterDefinition assigned — monster will do nothing.");
            return;
        }

        name = definition.displayName;
        transform.localScale = Vector3.one * definition.bodyScale;
        activeScale = transform.localScale;
        if (body != null) body.color = definition.bodyColor;
        health.SetMax(definition.maxHealth, refill: true);
        livesRemaining = definition.extraLives;

        // Gate is solid to ordinary monsters (a real physical backstop, so a
        // crowd pressing against one can never shove someone through it) but
        // must not be solid to a monster that's allowed through — and physics
        // layers can't express "block this layer except these specific
        // members" since every monster otherwise shares Enemy. Moving a
        // Passes Through Gates monster onto its own GatePasser layer (with a
        // collision-matrix exception against Gate) sidesteps that instead of
        // fighting it. EnemyCrowdMask above is what keeps this from also
        // splitting the crowd into two groups that ignore each other.
        if (definition.passesThroughGates)
        {
            int gatePasserLayer = LayerMask.NameToLayer("GatePasser");
            if (gatePasserLayer >= 0) gameObject.layer = gatePasserLayer;
        }
    }

    private void FixedUpdate()
    {
        var gm = GameManager.Instance;
        if (definition == null || bonePileActive || gm == null || gm.State != GameState.Playing)
        {
            if (rb.simulated) rb.linearVelocity = Vector2.zero; // avoid warning while a bone pile has physics off
            return;
        }

        Transform target = ChooseTarget(gm);

        if (target == null)
        {
            rb.linearVelocity = Vector2.zero;
            if (telegraph != null && telegraphPhase == TelegraphPhase.Winding) { telegraph.Cancel(); telegraphPhase = TelegraphPhase.Idle; }
            return;
        }

        // Route to it around any player-built maze. If every route is sealed,
        // whatever is doing the sealing becomes the target instead (§6,
        // "breakable if no path around it") — swapping the target here means
        // attack range, damage and the telegraph attack below all treat it as
        // an ordinary target with no special-casing of their own.
        target = ResolveNavigation(target);

        if (definition.usesTelegraphedAreaAttack)
        {
            UpdateTelegraphedAttack(gm, target);
            return;
        }

        // Attack Range only decides whether a hit can land right now — it is
        // NOT how close a monster is willing to get. A monster keeps trying
        // to advance every frame regardless of whether it's already in
        // range; physical collision (not this check) is what actually stops
        // it once there's nowhere left to go. This lets Attack Range stay a
        // real gameplay stat (matching the design doc's numbers) instead of
        // secretly doubling as "how close before it gives up approaching."
        float distanceToTarget = DistanceToTarget(target);
        if (distanceToTarget <= definition.attackRange)
        {
            TryAttack(target, gm);
        }
        else
        {
            // Not yet close enough to the real target — but if a structure
            // is literally blocking the direct path, hit that instead of
            // trying to walk through it.
            var blocking = NearestStructureWithin(definition.attackRange);
            if (blocking != null)
                TryAttack(blocking, gm);
        }

        MoveToward(target);
    }

    private void MoveToward(Transform target, float speedScale = 1f)
    {
        Vector2 steeringPoint = SteeringPoint(target);
        Vector2 desiredDirection = (steeringPoint - (Vector2)transform.position).normalized;
        rb.linearVelocity = SteerAroundNeighbors(desiredDirection) * (definition.moveSpeed * speedScale);
    }

    /// <summary>
    /// The point to actually steer at this frame — the next corner of the
    /// route, or the target itself when it's in plain sight.
    ///
    /// The line-of-sight check first is what keeps open ground feeling
    /// identical to before pathfinding existed: with nothing in the way a
    /// monster heads straight for the target's nearest surface point (so the
    /// crowd-spreading in ApproachPoint still works), and only walks the
    /// tile-by-tile route when something is genuinely between them.
    /// </summary>
    private Vector2 SteeringPoint(Transform target)
    {
        Vector2 approachPoint = ApproachPoint(target);
        var grid = PathGrid.Instance;
        if (grid == null || path.Count == 0) return approachPoint;

        // In plain sight — head straight for it. The route is kept rather than
        // discarded so it's still there the instant something comes between
        // them again (getting shoved back behind a corner, a wall going up)
        // instead of straight-lining into a wall until the next recalculation.
        if (grid.HasClearLine(transform.position, approachPoint, definition, target))
            return approachPoint;

        // Advance past waypoints already reached. Stopping one short of the end
        // keeps a waypoint to steer at until the target itself comes into
        // sight, which the check above then takes over for the final approach.
        float arrivalSqr = waypointArrivalRadius * waypointArrivalRadius;
        while (pathIndex < path.Count - 1 &&
               ((Vector2)transform.position - GridMath.TileCenterWorld(path[pathIndex])).sqrMagnitude < arrivalSqr)
            pathIndex++;

        if (pathIndex >= path.Count) return approachPoint;
        return GridMath.TileCenterWorld(path[pathIndex]);
    }

    /// <summary>
    /// Keeps this monster's route to its target current, and answers the one
    /// question routing exists to answer: can it actually get there?
    ///
    /// Returns the target unchanged when a route exists. When every route is
    /// sealed, returns the obstacle that has to be broken to open one — see
    /// PathGrid.Solve for why that obstacle is always genuinely part of the
    /// seal rather than just something standing nearby.
    ///
    /// Searches are rate-limited two ways: each monster only re-routes when
    /// something relevant changed (target, target's tile, or the obstacle
    /// layout) or its interval elapses, and PathGrid caps how many monsters may
    /// search in the same frame. A monster over that cap simply keeps walking
    /// its existing route.
    /// </summary>
    private Transform ResolveNavigation(Transform target)
    {
        var grid = PathGrid.Instance;
        if (grid == null) return target; // no routing in the scene — straight-line, as before

        // A structure being broken through can die mid-approach.
        if (blockedByStructure == null || !blockedByStructure.gameObject.activeInHierarchy)
            blockedByStructure = null;

        Vector2Int goalTile = GridMath.WorldToTile(target.position);
        bool stale = pathTarget != target ||
                     pathGridVersion != grid.Version ||
                     goalTile != pathGoalTile ||
                     Time.time >= nextRepathTime;

        if (stale && grid.TryBeginSearch())
        {
            pathTarget = target;
            pathGridVersion = grid.Version;
            pathGoalTile = goalTile;
            nextRepathTime = Time.time + repathInterval;
            pathIndex = 0;

            var outcome = grid.Solve(GridMath.WorldToTile(transform.position), goalTile,
                                     definition, target, path, out Transform blocker);
            blockedByStructure = outcome == PathGrid.PathOutcome.MustBreak ? blocker : null;
        }

        return blockedByStructure != null ? blockedByStructure : target;
    }

    /// <summary>
    /// Eases currentSpeedScale toward targetScale over rampSeconds (0 = snap
    /// instantly). Used to smoothly decelerate/accelerate the Cyclops around
    /// its telegraph wind-up instead of a stiff instant stop/start.
    /// </summary>
    private void UpdateSpeedScale(float targetScale, float rampSeconds)
    {
        if (rampSeconds <= 0f) { currentSpeedScale = targetScale; return; }
        float rate = 1f / rampSeconds;
        currentSpeedScale = Mathf.MoveTowards(currentSpeedScale, targetScale, rate * Time.fixedDeltaTime);
    }

    /// <summary>
    /// Telegraphed box attack (Cyclops §7.3): wind up (telegraph) → slam →
    /// cooldown → repeat. The box is aimed at and LOCKED over the target's
    /// position when the wind-up starts, so the target can dodge out of it.
    /// The slam damages everything caught inside the box.
    ///
    /// Movement ramp: when Pauses During Telegraph is on, currentSpeedScale
    /// eases from 1→0 over Telegraph Stop Duration as winding begins, and
    /// 0→1 over Telegraph Resume Duration once cooldown begins — deliberately
    /// its own timer, independent of Telegraph Time (the visual wind-up) and
    /// Attack Interval (the cooldown length), so tuning one never distorts
    /// the others. When the toggle is off, currentSpeedScale is just held at
    /// 1 and the Cyclops keeps walking normally through the whole attack —
    /// only the locked box position is affected.
    /// </summary>
    private void UpdateTelegraphedAttack(GameManager gm, Transform target)
    {
        switch (telegraphPhase)
        {
            case TelegraphPhase.Idle:
                if (DistanceToTarget(target) <= definition.attackRange)
                {
                    lockedBoxCenter = target.position; // aim + lock here
                    telegraph?.BeginTelegraph(lockedBoxCenter, definition.attackBoxSize,
                        definition.telegraphTime, definition.telegraphBaseColor, definition.telegraphFillColor);
                    telegraphPhase = TelegraphPhase.Winding;
                    telegraphPhaseEndTime = Time.time + definition.telegraphTime;
                }
                else
                {
                    if (!definition.pausesDuringTelegraph) currentSpeedScale = 1f;
                    else UpdateSpeedScale(1f, definition.telegraphResumeDuration);
                    MoveToward(target, currentSpeedScale); // not in range yet — approach
                }
                break;

            case TelegraphPhase.Winding:
                if (!definition.pausesDuringTelegraph)
                {
                    currentSpeedScale = 1f;
                    MoveToward(target, currentSpeedScale); // keeps walking; only the box is locked
                }
                else
                {
                    UpdateSpeedScale(0f, definition.telegraphStopDuration);
                    if (currentSpeedScale > 0.001f)
                        MoveToward(target, currentSpeedScale);
                    else
                        rb.linearVelocity = Vector2.zero;
                }

                if (Time.time >= telegraphPhaseEndTime)
                {
                    Slam(gm);
                    telegraph?.TriggerSlam(definition.slamColor, definition.slamFlashSeconds);
                    telegraphPhase = TelegraphPhase.Cooldown;
                    telegraphPhaseEndTime = Time.time + definition.attackInterval;
                }
                break;

            case TelegraphPhase.Cooldown:
                if (!definition.pausesDuringTelegraph) currentSpeedScale = 1f;
                else UpdateSpeedScale(1f, definition.telegraphResumeDuration);
                MoveToward(target, currentSpeedScale); // free to reposition between attacks
                if (Time.time >= telegraphPhaseEndTime)
                    telegraphPhase = TelegraphPhase.Idle;
                break;
        }
    }

    /// <summary>The slam: damage every non-monster Health inside the locked box once.</summary>
    private void Slam(GameManager gm)
    {
        var hits = Physics2D.OverlapBoxAll(lockedBoxCenter, definition.attackBoxSize, 0f);
        var damaged = new System.Collections.Generic.HashSet<Health>();
        foreach (var hit in hits)
        {
            if (hit.GetComponentInParent<MonsterAI>() != null) continue; // never hit other monsters (or self)
            var targetHealth = hit.GetComponentInParent<Health>();
            if (targetHealth == null || !damaged.Add(targetHealth)) continue;

            if (gm != null && targetHealth == gm.PlayerHealth) lastAttackedPlayerTime = Time.time;
            targetHealth.TakeDamage(DamageForHealth(targetHealth, hit.transform, gm));
        }
    }

    /// <summary>
    /// The point THIS monster should walk toward — the closest point on the
    /// target's own collider surface to its current position, not the
    /// target's center. Monsters approaching from different angles then
    /// naturally head to different points around a big target's perimeter
    /// instead of all converging on the exact same spot and piling up.
    /// </summary>
    private Vector2 ApproachPoint(Transform target)
    {
        var targetCollider = target.GetComponentInParent<Collider2D>();
        return targetCollider != null ? targetCollider.ClosestPoint(transform.position) : (Vector2)target.position;
    }

    /// <summary>
    /// If another monster is directly ahead within a short look-ahead
    /// distance, blend in a sideways nudge so this monster curves around it
    /// instead of walking straight into its back — the fix for monsters
    /// forming a single-file line behind whoever reached the target first.
    /// The side (left/right) is fixed per instance so it doesn't flicker.
    /// </summary>
    private Vector2 SteerAroundNeighbors(Vector2 desiredDirection)
    {
        var hit = Physics2D.CircleCast(transform.position, avoidanceProbeRadius, desiredDirection,
                                       avoidanceLookAhead, EnemyCrowdMask);
        if (hit.collider == null || hit.collider.gameObject == gameObject)
            return desiredDirection;

        Vector2 perpendicular = new Vector2(-desiredDirection.y, desiredDirection.x) * avoidSide;
        return (desiredDirection + perpendicular).normalized;
    }

    /// <summary>
    /// Full target selection, in strict priority order (no distance-based
    /// tiebreak between them — each rule either applies or it doesn't):
    /// 0. Recent-player-combat guard — if this monster attacked, or was
    ///    attacked by, the player within Recent Player Combat Window
    ///    seconds, structure-priority is skipped entirely this frame (both
    ///    tiers below), keeping it engaged with the player. NOT retroactive
    ///    in general: if the monster was ALREADY committed to a specific
    ///    structure target (committedStructureTarget), this guard normally
    ///    doesn't apply — getting hit mid-structure-attack doesn't pull it
    ///    back off. EXCEPTION: if it's also STUCK — physically blocked
    ///    (e.g. trapped behind another monster) from ever reaching that
    ///    committed structure, so it isn't actually accomplishing anything
    ///    by staying committed — the guard applies anyway, since there's no
    ///    real cost to switching to the player it can actually reach.
    /// 1a. Structure Priority Range (hard cutoff) — unconditional, always
    ///     wins outright when a structure is within range, beating both the
    ///     player AND the King. Cyclops's original behavior, unaffected by
    ///     anything else here.
    /// 1b. Structure Interest Range — a softer pull: prefer a structure over
    ///     heading to the King, but this ONLY ever competes with heading to
    ///     the King — if the base choice is already the player, this tier is
    ///     skipped entirely, no exceptions. The candidate must also be
    ///     closer to the King than this monster currently is (DistanceBetween),
    ///     so a detour can only ever be a step TOWARD the King, never
    ///     sideways or backward around the map.
    /// Both structure tiers also skip any candidate within Structure Near
    /// King Range of the King — too close to the King to bother attacking,
    /// go for the King directly instead.
    /// 2. King-priority — only ever a tiebreaker against CHASING THE PLAYER.
    ///    If a structure already won above, or the base choice wasn't the
    ///    player anyway, this never comes into play.
    /// 3. The base choice (PickTarget): player if in range, else the King.
    /// </summary>
    private Transform ChooseTarget(GameManager gm)
    {
        Transform baseTarget = PickTarget(gm);
        bool recentCombat = HasRecentPlayerCombat();

        bool kingInPriorityRange = gm.King != null && definition.kingPriorityRange > 0f &&
            DistanceToTarget(gm.King) <= definition.kingPriorityRange;

        // King-over-structures override: if enabled and the King is within
        // King Priority Range, the King beats structure-priority entirely.
        // Still yields to recent player combat UNLESS the monster is already
        // essentially on the King (within Keep Target range).
        if (definition.kingPriorityBeatsStructures && kingInPriorityRange &&
            !(recentCombat && !WithinKeepRange(gm.King)))
        {
            committedStructureTarget = null;
            return gm.King;
        }

        // "Stuck" = still committed to a structure, still outside attack
        // range of it (so not just successfully pressed up against it and
        // attacking), but actual physics velocity is near zero — meaning
        // something else (typically another monster) is blocking the way
        // and no progress is actually being made.
        bool isStuckOnCommittedStructure = committedStructureTarget != null &&
            rb.linearVelocity.sqrMagnitude < stuckVelocityThreshold * stuckVelocityThreshold &&
            DistanceToTarget(committedStructureTarget) > definition.attackRange;

        // Recent combat pulls the monster to the player instead of a
        // structure — but NOT if it's already committed (and not stuck), and
        // NOT if it's within Keep Target range of that structure (see below,
        // checked per-candidate since it needs the specific structure).
        bool recentCombatOverridesStructure = recentCombat &&
            (committedStructureTarget == null || isStuckOnCommittedStructure);

        if (definition.structurePriorityRange > 0f)
        {
            var closeStructure = NearestStructureWithin(definition.structurePriorityRange);
            if (closeStructure != null && !IsNearKing(closeStructure, gm) &&
                !(recentCombatOverridesStructure && !WithinKeepRange(closeStructure)))
            {
                committedStructureTarget = closeStructure;
                return closeStructure;
            }
        }

        if (baseTarget != gm.Player && definition.structureInterestRange > 0f && gm.King != null)
        {
            var nearbyStructure = NearestStructureWithin(definition.structureInterestRange);
            if (nearbyStructure != null && !IsNearKing(nearbyStructure, gm) &&
                !(recentCombatOverridesStructure && !WithinKeepRange(nearbyStructure)))
            {
                float structureDistanceToKing = DistanceBetween(nearbyStructure, gm.King);
                float monsterDistanceToKing = DistanceToTarget(gm.King);
                if (structureDistanceToKing < monsterDistanceToKing)
                {
                    committedStructureTarget = nearbyStructure;
                    return nearbyStructure;
                }
            }
        }

        committedStructureTarget = null;

        // King-Priority (vs chasing the player): also holds off during recent
        // player combat — otherwise a monster at the King's doorstep would get
        // yanked back to the King every frame even while actively fighting the
        // player hitting it. Exception: if it's within Keep Target range of the
        // King, it ignores the player aggro and stays on the King.
        if (baseTarget == gm.Player && kingInPriorityRange &&
            !(recentCombat && !WithinKeepRange(gm.King)))
            return gm.King;

        return baseTarget;
    }

    /// <summary>
    /// True if this monster is within Keep Target Within Range tiles (edge to
    /// edge) of the given target — used so recent-player-combat aggro can't
    /// pull a monster off a King/structure it's essentially already on.
    /// </summary>
    private bool WithinKeepRange(Transform target) =>
        definition.keepTargetWithinRange > 0f && target != null &&
        DistanceToTarget(target) <= definition.keepTargetWithinRange;

    /// <summary>
    /// True if the given structure is within Structure Near King Range of
    /// the King — used so a monster doesn't bother attacking something
    /// built right at the King's doorstep and goes straight for the King
    /// instead.
    /// </summary>
    private bool IsNearKing(Transform structure, GameManager gm) =>
        definition.structureNearKingRange > 0f && gm.King != null &&
        DistanceBetween(structure, gm.King) <= definition.structureNearKingRange;

    private Transform PickTarget(GameManager gm)
    {
        if (definition.targetsOnlyKing)
        {
            // Goblin: a Praise the King Tower within lure range wins over the King.
            if (definition.praiseTowerLureRange > 0f)
            {
                var hits = Physics2D.OverlapCircleAll(transform.position, definition.praiseTowerLureRange, structureLayers);
                Transform bestLure = null;
                float bestSqrDistance = float.MaxValue;
                foreach (var hit in hits)
                {
                    if (hit.GetComponentInParent<PraiseTheKingTower>() == null) continue;
                    float sqrDistance = ((Vector2)(hit.transform.position - transform.position)).sqrMagnitude;
                    if (sqrDistance < bestSqrDistance)
                    {
                        bestSqrDistance = sqrDistance;
                        bestLure = hit.transform;
                    }
                }
                if (bestLure != null) return bestLure;
            }
            return gm.King;
        }

        var player = gm.Player;
        bool playerAlive = gm.PlayerHealth != null && !gm.PlayerHealth.IsDead;
        if (player != null && playerAlive && definition.playerTargetRange > 0f &&
            Vector2.Distance(transform.position, player.position) <= definition.playerTargetRange)
            return player;

        return gm.King;
    }

    /// <summary>
    /// Nearest structure whose collider-to-collider GAP (not center distance,
    /// same edge-to-edge measure as DistanceToTarget) is within radius.
    /// Query a generously wide circle first (cheap broad-phase, still
    /// center-based but only used to shortlist candidates), then rank by the
    /// real edge distance so this agrees with the main attack-range check —
    /// otherwise a monster could think a big structure is "blocking" long
    /// before it's actually within its real attack range, or vice versa.
    ///
    /// Player-built Walls and Gates (anything with a Barrier component) are
    /// deliberately NOT candidates here. This feeds every DISCRETIONARY reason
    /// to attack a structure — the priority/interest ranges, and the
    /// "something's in my way" check — and barriers are exactly the thing a
    /// monster is supposed to route around rather than attack. Letting them in
    /// would have monsters grinding down the sides of a corridor simply for
    /// walking through it, which would make mazes pointless. A barrier only
    /// ever becomes a target through the no-route-exists fallback (§6,
    /// "breakable if no path around it"). A future monster designed to smash
    /// walls on sight would opt back in here rather than change that rule.
    /// </summary>
    private Transform NearestStructureWithin(float radius)
    {
        float queryRadius = radius + 3f; // margin generous enough to catch a 2x2 structure's far edge
        var hits = Physics2D.OverlapCircleAll(transform.position, queryRadius, structureLayers);
        Transform best = null;
        float bestDistance = float.MaxValue;
        foreach (var hit in hits)
        {
            if (hit.GetComponentInParent<Barrier>() != null) continue;
            float distance = DistanceBetween(transform, hit.transform);
            if (distance > radius) continue;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = hit.transform;
            }
        }
        return best;
    }

    private void TryAttack(Transform target, GameManager gm)
    {
        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + definition.attackInterval;

        if (gm != null && target == gm.Player)
            lastAttackedPlayerTime = Time.time;

        var targetHealth = target.GetComponentInParent<Health>();
        if (targetHealth != null)
            targetHealth.TakeDamage(DamageForHealth(targetHealth, target, gm));
    }

    /// <summary>
    /// True if this monster attacked the player, or the player attacked it,
    /// within Recent Player Combat Window seconds. Checked in ChooseTarget to
    /// keep a monster engaged with the player instead of letting it switch to
    /// a nearby structure mid-fight.
    /// </summary>
    private bool HasRecentPlayerCombat()
    {
        float window = definition.recentPlayerCombatWindow;
        if (window <= 0f) return false;

        if (Time.time - lastAttackedPlayerTime <= window) return true;
        if (health.LastDamageFromPlayer && Time.time - health.LastDamageTime <= window) return true;
        return false;
    }

    /// <summary>
    /// Damage this monster deals to a given target's Health. Compares against
    /// the King/Player Health objects (robust whether the hit landed on the
    /// root or a child collider). King Damage is its own value — always
    /// used, never a fallback to Player Damage — so a monster can hurt the
    /// King more or less than it hurts the player or structures.
    /// </summary>
    private float DamageForHealth(Health targetHealth, Transform hitTransform, GameManager gm)
    {
        if (gm != null && targetHealth == gm.KingHealth)
            return definition.kingDamage;

        if (gm != null && targetHealth == gm.PlayerHealth)
            return definition.playerDamage;

        if (definition.wallDamage > 0f && hitTransform.GetComponentInParent<Barrier>() != null)
            return definition.wallDamage;

        if (definition.praiseTowerDamage > 0f && hitTransform.GetComponentInParent<PraiseTheKingTower>() != null)
            return definition.praiseTowerDamage;

        return definition.structureDamage;
    }

    private void OnDied(Health _)
    {
        // Skeleton rule (§7.3): first death becomes an invulnerable bone pile
        // that revives — only the final death pays out and removes the monster.
        if (livesRemaining > 0)
        {
            livesRemaining--;
            StartCoroutine(BonePileRoutine());
            return;
        }

        if (currencyDropPrefab != null && definition != null)
        {
            var drop = Instantiate(currencyDropPrefab, transform.position, Quaternion.identity);
            drop.SetValue(definition.currencyDrop);
        }

        Killed?.Invoke(this);
        Destroy(gameObject);
    }

    private IEnumerator BonePileRoutine()
    {
        bonePileActive = true;
        health.Invulnerable = true;

        // Freeze physics entirely: rb.simulated = false zeroes its velocity,
        // stops it being shoved by other monsters, AND removes it from
        // physics queries so towers/the sword don't target the pile. This is
        // what actually keeps it from drifting/creeping while "invulnerable".
        rb.simulated = false;
        foreach (var col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;

        // Hide the health bar while piled (otherwise a 0-width sliver lingers),
        // and show the squashed, recolored bone pile.
        SetHealthBarsVisible(false);
        if (body != null) body.color = definition.bonePileColor;
        transform.localScale = new Vector3(activeScale.x, activeScale.y * 0.35f, activeScale.z);

        yield return new WaitForSeconds(definition.reviveDelaySeconds);

        transform.localScale = activeScale;
        if (body != null) body.color = definition.bodyColor;
        foreach (var col in GetComponentsInChildren<Collider2D>())
            col.enabled = true;
        rb.simulated = true;
        health.ResetToFull();
        health.Invulnerable = false;
        SetHealthBarsVisible(true);
        bonePileActive = false;
    }

    private void SetHealthBarsVisible(bool visible)
    {
        foreach (var bar in GetComponentsInChildren<HealthBar>(true))
            bar.gameObject.SetActive(visible);
    }
}
