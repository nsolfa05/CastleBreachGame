using UnityEngine;

/// <summary>
/// The vertical slice's one monster: the Zombie (design doc §7.3).
/// Behavior: walks straight toward the King; if the player comes within
/// 6 tiles, chases the player instead. Attacks whatever it reaches —
/// including structures (towers/walls) that block its path.
///
/// NOTE: movement is straight-line for the slice. Real pathfinding around
/// player-built wall mazes is a post-slice feature.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class ZombieAI : MonoBehaviour
{
    [Header("Stats (design doc §7.3 — Zombie column)")]
    [SerializeField] private float moveSpeed = 4f;

    [Tooltip("Chases the player when they are within this many tiles.")]
    [SerializeField] private float playerTargetRange = 6f;

    [Tooltip("Distance (center to center) at which the zombie can hit its target. Slightly over 1 tile so it doesn't need to overlap.")]
    [SerializeField] private float attackRange = 1.2f;

    [SerializeField] private float attackDamage = 3f;
    [SerializeField] private float structureDamage = 3f;

    [Tooltip("Seconds between attacks (doc: one hit every 1.5s).")]
    [SerializeField] private float attackInterval = 1.5f;

    [Header("Currency (§4 / §7.3)")]
    [SerializeField] private int currencyDropAmount = 3;
    [SerializeField] private CurrencyDrop currencyDropPrefab;

    [Header("Targeting")]
    [Tooltip("Layers holding player-built structures — attacked when they're in reach (e.g. a tower in the way). Set to the Structure layer.")]
    [SerializeField] private LayerMask structureLayers;

    private Rigidbody2D rb;
    private Health health;
    private float nextAttackTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        health.Died += OnDied;
    }

    private void FixedUpdate()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Playing)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Transform target = PickTarget(gm);
        if (target == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float distance = Vector2.Distance(transform.position, target.position);
        if (distance <= attackRange)
        {
            rb.linearVelocity = Vector2.zero;
            TryAttack(target, isStructure: false);
        }
        else
        {
            // A structure within reach (blocking the path) gets attacked first.
            var structure = NearestStructureInReach();
            if (structure != null)
            {
                rb.linearVelocity = Vector2.zero;
                TryAttack(structure, isStructure: true);
            }
            else
            {
                rb.linearVelocity = ((Vector2)(target.position - transform.position)).normalized * moveSpeed;
            }
        }
    }

    private Transform PickTarget(GameManager gm)
    {
        var player = gm.Player;
        bool playerAlive = gm.PlayerHealth != null && !gm.PlayerHealth.IsDead;
        if (player != null && playerAlive &&
            Vector2.Distance(transform.position, player.position) <= playerTargetRange)
            return player;

        return gm.King;
    }

    private Transform NearestStructureInReach()
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, attackRange, structureLayers);
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

    private void TryAttack(Transform target, bool isStructure)
    {
        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + attackInterval;

        var targetHealth = target.GetComponentInParent<Health>();
        if (targetHealth != null)
            targetHealth.TakeDamage(isStructure ? structureDamage : attackDamage);
    }

    private void OnDied(Health _)
    {
        if (currencyDropPrefab != null)
        {
            var drop = Instantiate(currencyDropPrefab, transform.position, Quaternion.identity);
            drop.SetValue(currencyDropAmount);
        }
        Destroy(gameObject);
    }
}
