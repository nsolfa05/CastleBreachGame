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

    /// <summary>Called by the WaveSpawner right after Instantiate, before the first frame.</summary>
    public void SetDefinition(MonsterDefinition newDefinition) => definition = newDefinition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        myCollider = GetComponent<Collider2D>();
        health.Died += OnDied;
        if (body == null) body = GetComponent<SpriteRenderer>();
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

        float distance = DistanceToTarget(target);
        if (distance <= definition.attackRange)
        {
            rb.linearVelocity = Vector2.zero;
            TryAttack(target, gm);
        }
        else
        {
            // A structure within reach (blocking the path) gets attacked first.
            var blocking = NearestStructureWithin(definition.attackRange);
            if (blocking != null)
            {
                rb.linearVelocity = Vector2.zero;
                TryAttack(blocking, gm);
            }
            else
            {
                rb.linearVelocity = ((Vector2)(target.position - transform.position)).normalized * definition.moveSpeed;
            }
        }
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

    private Transform NearestStructureWithin(float radius)
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, radius, structureLayers);
        Transform best = null;
        float bestSqrDistance = float.MaxValue;
        foreach (var hit in hits)
        {
            float sqrDistance = ((Vector2)(hit.transform.position - transform.position)).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
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
