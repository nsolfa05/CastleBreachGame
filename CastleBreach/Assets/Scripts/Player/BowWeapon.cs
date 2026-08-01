using UnityEngine;

/// <summary>
/// The Bow — charge-and-release ranged weapon (Guide 11b). Hold Space to draw
/// (Wind Up Time, default 2s — see ChargedWeapon), release to fire a
/// StraightProjectile that flies in a straight line, stopping on the first
/// enemy hit or after Range tiles, whichever comes first. No knockback or
/// stun by default (per the design brief), though Arrow Effects is still a
/// normal editable HitEffects field like every other attack in the framework.
/// Ignores walls/structures in flight — see StraightProjectile.
/// </summary>
public class BowWeapon : ChargedWeapon
{
    [Header("Bow (Guide 11b)")]
    [SerializeField] private float damage = 3f;
    [Tooltip("Max travel distance (tiles) before the arrow disappears unused.")]
    [SerializeField] private float range = 6f;
    [Tooltip("How fast the arrow travels (tiles/second) — also how easy the shot is to dodge.")]
    [SerializeField] private float projectileSpeed = 10f;
    [Tooltip("What the arrow can hit — set this to the Enemy layer.")]
    [SerializeField] private LayerMask hitLayers;
    [Tooltip("Knockback / stun the arrow applies on hit — off by default per the design brief, but still fully editable like every attack in the framework.")]
    [SerializeField] private HitEffects arrowEffects;

    [Header("Visuals [Placeholder]")]
    [SerializeField] private Sprite arrowSprite;
    [SerializeField] private Color arrowColor = Color.white;
    [Tooltip("Arrow sprite footprint (length, width) in tiles.")]
    [SerializeField] private Vector2 arrowSize = new Vector2(0.5f, 0.12f);

    protected override Vector2 ChargeBarSize => new Vector2(range, 0.3f);

    protected override void Awake()
    {
        base.Awake();
        // A gate-passing monster (the Goblin) spawns onto the GatePasser layer,
        // not Enemy — without this an arrow would silently pass through it.
        hitLayers = MonsterLayers.IncludeGatePasser(hitLayers);
    }

    protected override void Fire(Vector2 origin, Vector2 direction)
    {
        var go = new GameObject("Arrow");
        go.transform.position = origin;
        go.transform.localScale = new Vector3(arrowSize.x, arrowSize.y, 1f);

        if (arrowSprite != null)
        {
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = arrowSprite;
            renderer.color = arrowColor;
            renderer.sortingOrder = 25;
        }

        var projectile = go.AddComponent<StraightProjectile>();
        projectile.Launch(direction, projectileSpeed, range, hitLayers, (point, hitCollider) =>
        {
            if (hitCollider == null) return; // reached max range with nothing in the way — just vanishes
            var health = hitCollider.GetComponentInParent<Health>();
            if (health == null) return;
            health.TakeDamage(damage, fromPlayer: true);
            arrowEffects.ApplyTo(hitCollider, origin);
        });
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 dir = (Application.isPlaying && aim != null) ? aim.AimDirection : Vector2.right;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, (Vector2)transform.position + dir * range);
    }
}
