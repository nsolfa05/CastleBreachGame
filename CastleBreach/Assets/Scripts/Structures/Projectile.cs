using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple homing projectile [Placeholder]: flies toward its target and deals
/// damage on arrival. If the target dies mid-flight, the projectile
/// disappears. Supports optional splash damage (Catapult): everything on the
/// splash layers within the radius of the impact point takes the damage too.
/// The prefab's Speed field doubles as "hang time" — the Catapult uses a
/// slow stone so shots arc in lazily (doc: projectile hangs ~2s). A splash
/// hit also drops a brief ImpactMark circle sized to Splash Radius, so it's
/// visible exactly where the blast landed and how far it reached.
/// </summary>
public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 12f;
    [SerializeField] private float hitDistance = 0.2f;

    [Header("Impact mark [Placeholder] — optional")]
    [Tooltip("Circle sprite marking where this shot landed, sized to match Splash Radius. Only shown for splash hits (Splash Radius > 0) — a single-target hit is already obvious from the impact itself. Leave empty to skip it.")]
    [SerializeField] private Sprite impactMarkSprite;
    [Tooltip("Color of the impact mark.")]
    [SerializeField] private Color impactMarkColor = new Color(0.35f, 0.3f, 0.25f, 0.55f);
    [Tooltip("How long the impact mark stays visible, in seconds.")]
    [SerializeField] private float impactMarkSeconds = 0.4f;
    [Tooltip("Draw order for the impact mark. Low (4) = a ground marker beneath structures/characters.")]
    [SerializeField] private int impactMarkSortingOrder = 4;

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
            ImpactMark.Spawn(transform.position, impactMarkSprite, impactMarkColor,
                splashRadius * 2f, impactMarkSeconds, impactMarkSortingOrder);

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
