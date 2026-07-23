using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple homing projectile [Placeholder]: flies toward its target and deals
/// damage on arrival. If the target dies mid-flight, the projectile
/// disappears. Supports optional splash damage (Catapult): everything on the
/// splash layers within the radius of the impact point takes the damage too.
/// The prefab's Speed field doubles as "hang time" — the Catapult uses a
/// slow stone so shots arc in lazily (doc: projectile hangs ~2s).
/// </summary>
public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 12f;
    [SerializeField] private float hitDistance = 0.2f;

    private Transform target;
    private float damage;
    private float splashRadius;
    private LayerMask splashLayers;

    public void Launch(Transform newTarget, float newDamage)
    {
        Launch(newTarget, newDamage, 0f, 0);
    }

    public void Launch(Transform newTarget, float newDamage, float newSplashRadius, LayerMask newSplashLayers)
    {
        target = newTarget;
        damage = newDamage;
        splashRadius = newSplashRadius;
        splashLayers = newSplashLayers;
    }

    private void Update()
    {
        if (target == null) // target was destroyed mid-flight
        {
            Destroy(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        if (((Vector2)(target.position - transform.position)).sqrMagnitude <= hitDistance * hitDistance)
        {
            Impact();
            Destroy(gameObject);
        }
    }

    private void Impact()
    {
        if (splashRadius > 0f)
        {
            // Area damage: hit every distinct Health in the blast radius once.
            var hits = Physics2D.OverlapCircleAll(transform.position, splashRadius, splashLayers);
            var alreadyDamaged = new HashSet<Health>();
            foreach (var hit in hits)
            {
                var health = hit.GetComponentInParent<Health>();
                if (health != null && alreadyDamaged.Add(health))
                    health.TakeDamage(damage);
            }
        }
        else
        {
            var health = target.GetComponentInParent<Health>();
            if (health != null)
                health.TakeDamage(damage);
        }
    }
}
