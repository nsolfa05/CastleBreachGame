using UnityEngine;

/// <summary>
/// Simple homing projectile [Placeholder]: flies toward its target and
/// deals damage on arrival. If the target dies mid-flight, the projectile
/// disappears. No physics needed — it just moves in a straight line
/// toward wherever the target currently is.
/// </summary>
public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 12f;
    [SerializeField] private float hitDistance = 0.2f;

    private Transform target;
    private float damage;

    public void Launch(Transform newTarget, float newDamage)
    {
        target = newTarget;
        damage = newDamage;
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
            var health = target.GetComponentInParent<Health>();
            if (health != null)
                health.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
