using UnityEngine;

/// <summary>
/// The shared combat-tower brain (design doc §6) — one script drives every
/// attacking tower type; only the Inspector numbers differ per prefab:
/// - Archer Tower: range 6, 4 dmg, 1s, projectile, single target
/// - Pike Tower:   range 1.5, 2 dmg, 1s, NO projectile (leave the field
///                 empty = instant melee hit), single target
/// - Catapult:     range 7, 3 dmg, 5s, slow projectile, Min Range 3 (dead
///                 zone — can't hit close enemies), Splash Radius 1 (AoE)
///
/// Targeting is selectable per tower: retarget the nearest enemy every shot,
/// or lock on until the current target dies or leaves range.
/// Its Health/collider live on the same object; DestroyWhenDead removes it.
/// (This file was previously named ArcherTower.cs.)
/// </summary>
public class AttackTower : MonoBehaviour
{
    /// <summary>How the tower chooses what to shoot each time it fires.</summary>
    public enum TargetingMode
    {
        [Tooltip("Re-pick the nearest enemy in range every shot (switches to whatever's closest/most threatening).")]
        NearestEachShot,

        [Tooltip("Keep firing at the current target until it dies or leaves range, then pick the nearest.")]
        FinishCurrentTarget,
    }

    [Header("Stats (design doc §6)")]
    [SerializeField] private float damage = 4f;
    [SerializeField] private float secondsBetweenShots = 1f;

    [Tooltip("Targeting radius in tiles, measured from the tower's center.")]
    [SerializeField] private float range = 6f;

    [Tooltip("Dead zone: enemies closer than this are ignored (Catapult ~3). 0 = none.")]
    [SerializeField] private float minRange = 0f;

    [Tooltip("Area damage radius at the point of impact, in tiles (Catapult ~1). 0 = single target. Needs a projectile.")]
    [SerializeField] private float splashRadius = 0f;

    [Tooltip("Which layers count as enemies — set to the Enemy layer.")]
    [SerializeField] private LayerMask enemyLayers;

    [Tooltip("Nearest Each Shot = always retarget the closest enemy. " +
             "Finish Current Target = lock on until the current enemy dies or leaves range.")]
    [SerializeField] private TargetingMode targeting = TargetingMode.NearestEachShot;

    [Header("References")]
    [Tooltip("Optional: leave EMPTY for an instant melee hit (Pike Tower).")]
    [SerializeField] private Projectile projectilePrefab;

    [Tooltip("Where projectiles spawn from (a child at the top of the tower). Uses the tower's center if empty.")]
    [SerializeField] private Transform firePoint;

    private float nextShotTime;
    private Transform currentTarget;

    /// <summary>Attack radius in tiles — read by TowerRangeCircle to size its indicator.</summary>
    public float Range => range;

    /// <summary>Dead-zone radius in tiles — read by TowerRangeCircle for the inner circle.</summary>
    public float MinRange => minRange;

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.State != GameState.Playing) return;
        if (Time.time < nextShotTime) return;

        var target = ChooseTarget();
        if (target == null) return;

        currentTarget = target;
        nextShotTime = Time.time + secondsBetweenShots;

        if (projectilePrefab != null)
        {
            Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
            var projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
            projectile.Launch(target, damage, splashRadius, enemyLayers);
        }
        else
        {
            // Melee tower (Pike): no projectile, the hit lands instantly.
            var targetHealth = target.GetComponentInParent<Health>();
            if (targetHealth != null)
                targetHealth.TakeDamage(damage);
        }
    }

    private Transform ChooseTarget()
    {
        // Finish-current mode: stick with the current target while it's still
        // alive, hittable, and inside the ring between minRange and range.
        if (targeting == TargetingMode.FinishCurrentTarget && IsValidTarget(currentTarget))
        {
            float sqrDistance = ((Vector2)(currentTarget.position - transform.position)).sqrMagnitude;
            if (sqrDistance <= range * range && sqrDistance >= minRange * minRange)
                return currentTarget;
        }

        return FindNearestEnemy();
    }

    private bool IsValidTarget(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        var health = target.GetComponentInParent<Health>();
        return health != null && !health.IsDead && !health.Invulnerable;
    }

    private Transform FindNearestEnemy()
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, range, enemyLayers);
        Transform nearest = null;
        float bestSqrDistance = float.MaxValue;
        float minSqr = minRange * minRange;
        foreach (var hit in hits)
        {
            float sqrDistance = ((Vector2)(hit.transform.position - transform.position)).sqrMagnitude;
            if (sqrDistance < minSqr) continue;          // inside the dead zone
            if (!IsValidTarget(hit.transform)) continue; // dead or invulnerable (bone pile)
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                nearest = hit.transform;
            }
        }
        return nearest;
    }

    // Shows the targeting radius (and dead zone) in the Scene view while selected.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, range);
        if (minRange > 0f)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, minRange);
        }
    }
}
