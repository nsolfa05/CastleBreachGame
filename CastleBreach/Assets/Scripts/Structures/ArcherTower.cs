using UnityEngine;

/// <summary>
/// Archer Tower (design doc §6): fires one projectile per second at an enemy
/// in range, 4 damage, single target. Targeting is selectable in the
/// Inspector — retarget the nearest enemy every shot, or lock onto one until
/// it dies/leaves range (see <see cref="TargetingMode"/>). Its Health/collider
/// live on the same object; DestroyWhenDead removes it when destroyed.
/// </summary>
public class ArcherTower : MonoBehaviour
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

    [Tooltip("Which layers count as enemies — set to the Enemy layer.")]
    [SerializeField] private LayerMask enemyLayers;

    [Tooltip("Nearest Each Shot = always retarget the closest enemy. " +
             "Finish Current Target = lock on until the current enemy dies or leaves range.")]
    [SerializeField] private TargetingMode targeting = TargetingMode.NearestEachShot;

    [Header("References")]
    [SerializeField] private Projectile projectilePrefab;

    [Tooltip("Where arrows spawn from (a child at the top of the tower). Uses the tower's center if empty.")]
    [SerializeField] private Transform firePoint;

    private float nextShotTime;
    private Transform currentTarget;

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.State != GameState.Playing) return;
        if (Time.time < nextShotTime) return;

        var target = ChooseTarget();
        if (target == null) return;

        currentTarget = target;
        nextShotTime = Time.time + secondsBetweenShots;
        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
        var projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        projectile.Launch(target, damage);
    }

    private Transform ChooseTarget()
    {
        // Finish-current mode: stick with the current target while it's still
        // alive (not destroyed) and inside range; otherwise fall through to
        // picking the nearest.
        if (targeting == TargetingMode.FinishCurrentTarget &&
            currentTarget != null &&
            ((Vector2)(currentTarget.position - transform.position)).sqrMagnitude <= range * range)
            return currentTarget;

        return FindNearestEnemy();
    }

    private Transform FindNearestEnemy()
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, range, enemyLayers);
        Transform nearest = null;
        float bestSqrDistance = float.MaxValue;
        foreach (var hit in hits)
        {
            float sqrDistance = ((Vector2)(hit.transform.position - transform.position)).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                nearest = hit.transform;
            }
        }
        return nearest;
    }

    // Shows the targeting radius in the Scene view while the tower is selected.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
