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
/// - prioritizesStructures within a radius (Cyclops; also usable on any
///   monster) — beats BOTH the player and the King unconditionally
/// - extraLives + invulnerable bone-pile revive (Skeleton)
///
/// NOTE: movement is still straight-line. Real pathfinding around
/// player-built wall mazes is the walls/pathfinding phase.
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

    /// <summary>Fired exactly once, when the monster is permanently dead
    /// (a Skeleton's first death does NOT fire this — it revives).</summary>
    public event Action<MonsterAI> Killed;

    public MonsterDefinition Definition => definition;

    private Rigidbody2D rb;
    private Health health;
    private Collider2D myCollider;
    private float nextAttackTime;
    private int livesRemaining;
    private bool bonePileActive;
    private Vector3 activeScale;
    private float avoidSide; // -1 or +1, fixed per instance so avoidance doesn't flicker sides
    private int enemyLayerMask;

    /// <summary>Called by the WaveSpawner right after Instantiate, before the first frame.</summary>
    public void SetDefinition(MonsterDefinition newDefinition) => definition = newDefinition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        myCollider = GetComponent<Collider2D>();
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
    private float DistanceToTarget(Transform target)
    {
        var targetCollider = target.GetComponentInParent<Collider2D>();
        if (myCollider != null && targetCollider != null)
            return myCollider.Distance(targetCollider).distance;
        return Vector2.Distance(transform.position, target.position);
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
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Transform target = ChooseTarget(gm);

        if (target == null)
        {
            rb.linearVelocity = Vector2.zero;
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

        Vector2 approachPoint = ApproachPoint(target);
        Vector2 desiredDirection = (approachPoint - (Vector2)transform.position).normalized;
        rb.linearVelocity = SteerAroundNeighbors(desiredDirection) * definition.moveSpeed;
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
    /// Full target selection: the base choice (PickTarget) can be overridden
    /// by two independent proximity rules — being near the King, or being
    /// near a structure. King-proximity only ever competes with "chasing the
    /// player" (if the base choice was already the King, there's nothing to
    /// change); structure-proximity keeps its original unconditional
    /// behavior (beats the player OR the King, same as Cyclops always did).
    /// If both proximity rules trigger at once, whichever candidate is
    /// physically closer wins.
    /// </summary>
    private Transform ChooseTarget(GameManager gm)
    {
        Transform baseTarget = PickTarget(gm);

        Transform kingCandidate = null;
        float kingDistance = float.MaxValue;
        if (baseTarget == gm.Player && gm.King != null && definition.kingPriorityRange > 0f)
        {
            float distance = DistanceToTarget(gm.King);
            if (distance <= definition.kingPriorityRange)
            {
                kingCandidate = gm.King;
                kingDistance = distance;
            }
        }

        Transform structureCandidate = null;
        float structureDistance = float.MaxValue;
        if (definition.prioritizesStructures && definition.structurePriorityRange > 0f)
        {
            var nearest = NearestStructureWithin(definition.structurePriorityRange);
            if (nearest != null)
            {
                structureCandidate = nearest;
                structureDistance = DistanceToTarget(nearest);
            }
        }

        if (kingCandidate != null && structureCandidate != null)
            return kingDistance <= structureDistance ? kingCandidate : structureCandidate;
        if (structureCandidate != null) return structureCandidate;
        if (kingCandidate != null) return kingCandidate;

        return baseTarget;
    }

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
            float distance = myCollider != null
                ? myCollider.Distance(hit).distance
                : Vector2.Distance(transform.position, hit.transform.position);
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

        var targetHealth = target.GetComponentInParent<Health>();
        if (targetHealth != null)
            targetHealth.TakeDamage(DamageFor(target, gm));
    }

    private float DamageFor(Transform target, GameManager gm)
    {
        if (gm != null && (target == gm.Player || target == gm.King))
            return definition.playerDamage;

        if (definition.praiseTowerDamage > 0f && target.GetComponentInParent<PraiseTheKingTower>() != null)
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
        rb.linearVelocity = Vector2.zero;

        // Invisible to towers/the player's sword while piled (their target
        // searches go through colliders), and visibly squashed + recolored.
        foreach (var col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;
        if (body != null) body.color = definition.bonePileColor;
        transform.localScale = new Vector3(activeScale.x, activeScale.y * 0.35f, activeScale.z);

        yield return new WaitForSeconds(definition.reviveDelaySeconds);

        transform.localScale = activeScale;
        if (body != null) body.color = definition.bodyColor;
        foreach (var col in GetComponentsInChildren<Collider2D>())
            col.enabled = true;
        health.ResetToFull();
        health.Invulnerable = false;
        bonePileActive = false;
    }
}
