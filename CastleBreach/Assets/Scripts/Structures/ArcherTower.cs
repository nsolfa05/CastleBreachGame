using UnityEngine;

/// <summary>
/// Archer Tower (design doc §6): fires one projectile per second at the
/// nearest enemy in range, 4 damage, single target. Its Health/collider
/// live on the same object; DestroyWhenDead removes it when destroyed.
/// </summary>
public class ArcherTower : MonoBehaviour
{
    [Header("Stats (design doc §6)")]
    [SerializeField] private float damage = 4f;
    [SerializeField] private float secondsBetweenShots = 1f;

    [Tooltip("Targeting radius in tiles, measured from the tower's center.")]
    [SerializeField] private float range = 6f;

    [Tooltip("Which layers count as enemies — set to the Enemy layer.")]
    [SerializeField] private LayerMask enemyLayers;

    [Header("References")]
    [SerializeField] private Projectile projectilePrefab;

    [Tooltip("Where arrows spawn from (a child at the top of the tower). Uses the tower's center if empty.")]
    [SerializeField] private Transform firePoint;

    private float nextShotTime;

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.State != GameState.Playing) return;
        if (Time.time < nextShotTime) return;

        var target = FindNearestEnemy();
        if (target == null) return;

        nextShotTime = Time.time + secondsBetweenShots;
        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
        var projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        projectile.Launch(target, damage);
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
