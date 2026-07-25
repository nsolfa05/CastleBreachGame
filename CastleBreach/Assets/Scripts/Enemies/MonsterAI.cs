using System;
using System.Collections;
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
/// NOTE: movement is still straight-line. Real pathfinding around
/// player-built wall mazes is the walls/pathfinding phase — DistanceBetween
/// below is the single choke point to swap for real path length once that
/// lands, so every distance-based targeting rule (including the King-
/// progress check in Structure Interest Range) becomes pathfinding-aware
/// with no other changes needed.
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
    private int enemyLayerMask;
    private float lastAttackedPlayerTime = float.NegativeInfinity;
    private Transform committedStructureTarget; // non-null while already committed to attacking a specific structure
    private TelegraphPhase telegraphPhase = TelegraphPhase.Idle;
    private float telegraphPhaseEndTime;
    private Vector2 lockedBoxCenter;
    private float currentSpeedScale = 1f; // telegraph-attack movement ramp (1 = full speed, 0 = fully stopped)

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
        enemyLayerMask = 1 << gameObject.layer;
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
    /// transform distance if either side lacks a Collider2D). This is the
    /// ONE place every distance-based targeting decision measures "how far
    /// apart are these two things" — currently straight-line, since there's
    /// nothing to route around yet (movement is still straight-line, per the
    /// class note above). When real pathfinding (walls/gates, §7.1) lands,
    /// swap this body for actual path length and every rule built on it —
    /// attack range, Structure Priority/Interest Range, Structure Near King
    /// Range, the King-progress check inside Structure Interest Range — all
    /// automatically become pathfinding-aware with no other changes needed.
    /// In particular, that's what makes the King-progress check correctly
    /// react to a broken wall: once a shortcut opens, any structure along
    /// the new shortest route recalculates as closer-to-the-King and starts
    /// qualifying again, with zero logic changes required elsewhere.
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
        Vector2 approachPoint = ApproachPoint(target);
        Vector2 desiredDirection = (approachPoint - (Vector2)transform.position).normalized;
        rb.linearVelocity = SteerAroundNeighbors(desiredDirection) * (definition.moveSpeed * speedScale);
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
                                       avoidanceLookAhead, enemyLayerMask);
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
    /// </summary>
    private Transform NearestStructureWithin(float radius)
    {
        float queryRadius = radius + 3f; // margin generous enough to catch a 2x2 structure's far edge
        var hits = Physics2D.OverlapCircleAll(transform.position, queryRadius, structureLayers);
        Transform best = null;
        float bestDistance = float.MaxValue;
        foreach (var hit in hits)
        {
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
