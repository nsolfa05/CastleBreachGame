using UnityEngine;

/// <summary>
/// A projectile that flies in a fixed straight line from launch (unlike the
/// existing homing Projectile used by towers, which continuously steers toward
/// a target Transform) — used by the Bow and Fire Staff. Travels until it hits
/// something on Hit Layers or reaches Max Range, then fires an impact callback
/// and destroys itself. Deliberately has no idea about walls/structures: the
/// caller controls what "hits" it by choosing Hit Layers (Enemy only, never
/// Structure/King), so it naturally flies straight through them — that's the
/// "ignores walls" behavior both weapons want, with no special-case code here.
///
/// Uses a swept circle-cast each frame (not a plain position check) so a fast
/// arrow can't tunnel through a thin collider between two frames.
/// </summary>
public class StraightProjectile : MonoBehaviour
{
    [Tooltip("Radius of the sweep used to detect a hit each frame — roughly the arrow/bolt's own width.")]
    [SerializeField] private float hitRadius = 0.15f;

    private Vector2 direction;
    private float speed;
    private float maxRange;
    private float traveled;
    private LayerMask hitLayers;
    private System.Action<Vector2, Collider2D> onImpact;

    /// <summary>Fire this projectile. onImpact is called once, with the impact
    /// point and the collider it hit (null if it simply reached Max Range with
    /// nothing in the way), right before this object destroys itself.</summary>
    public void Launch(Vector2 travelDirection, float projectileSpeed, float range,
                       LayerMask layers, System.Action<Vector2, Collider2D> impactCallback)
    {
        direction = travelDirection.sqrMagnitude > 0.0001f ? travelDirection.normalized : Vector2.right;
        speed = Mathf.Max(0.01f, projectileSpeed);
        maxRange = range;
        hitLayers = layers;
        onImpact = impactCallback;
        traveled = 0f;

        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    private void Update()
    {
        float step = speed * Time.deltaTime;
        Vector2 origin = transform.position;

        // Swept test for this frame's travel distance, not a single point check —
        // catches a hit even if the target is thinner than one frame's movement.
        var hit = Physics2D.CircleCast(origin, hitRadius, direction, step, hitLayers);
        if (hit.collider != null)
        {
            Impact(hit.point, hit.collider);
            return;
        }

        traveled += step;
        transform.position = origin + direction * step;

        if (traveled >= maxRange)
            Impact(transform.position, null);
    }

    private void Impact(Vector2 point, Collider2D hitCollider)
    {
        onImpact?.Invoke(point, hitCollider);
        Destroy(gameObject);
    }
}
