using UnityEngine;

/// <summary>
/// The Hammer — charge-and-release melee weapon (Guide 11b). Hold Space to
/// wind up, release to slam an AREA in front of the player at Reach tiles
/// out, sized Hit Size (tiles) — unlike the Sword's angular arc, this is a
/// fixed box, since a hammer slam is a single heavy impact zone rather than a
/// sweeping cut. Every monster caught in the box also gets
/// MonsterAI.NotifyForcedPlayerEngagement() — the "distract" half of the
/// design brief: a slam pulls a monster's attention onto the player even on a
/// glancing hit, whether or not Knockback/Stun are also turned on.
/// </summary>
public class HammerWeapon : ChargedWeapon
{
    [Header("Hammer (Guide 11b)")]
    [SerializeField] private float damage = 6f;

    [Tooltip("How far ahead of the player the slam lands (tiles) — the box's center distance.")]
    [SerializeField] private float reach = 1.5f;

    [Tooltip("Size (width, height) of the slam's hit box in tiles.")]
    [SerializeField] private Vector2 hitSize = new Vector2(1f, 1f);

    [Tooltip("What the slam can hit — set this to the Enemy layer.")]
    [SerializeField] private LayerMask hitLayers;

    [Tooltip("Layers that BLOCK the slam — set to Structure + King, same as the Sword. An enemy on the far side of a wall from the player is spared.")]
    [SerializeField] private LayerMask obstructionLayers;

    [Tooltip("Knockback / stun this slam applies on hit — a heavy hit, so a strong knockback fits the fantasy, but off by default like every attack in the framework.")]
    [SerializeField] private HitEffects hammerEffects;

    [Header("Visuals [Placeholder]")]
    [SerializeField] private SpriteRenderer slamVisual;
    [SerializeField] private float slamFlashSeconds = 0.15f;

    private float slamVisualOffTime;

    protected override Vector2 ChargeBarSize => new Vector2(reach, hitSize.y);

    protected override void Awake()
    {
        base.Awake();
        if (slamVisual != null) slamVisual.enabled = false;
        // A gate-passing monster (the Goblin) spawns onto the GatePasser layer,
        // not Enemy — without this the slam would silently miss it.
        hitLayers = MonsterLayers.IncludeGatePasser(hitLayers);
    }

    protected override void Update()
    {
        base.Update();
        if (slamVisual != null && slamVisual.enabled && Time.time >= slamVisualOffTime)
            slamVisual.enabled = false;
    }

    protected override void Fire(Vector2 origin, Vector2 direction)
    {
        if (slamVisual != null)
        {
            slamVisual.enabled = true;
            slamVisualOffTime = Time.time + slamFlashSeconds;
        }

        Vector2 center = origin + direction * reach;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        var hits = Physics2D.OverlapBoxAll(center, hitSize, angle, hitLayers);
        foreach (var hit in hits)
        {
            var health = hit.GetComponentInParent<Health>();
            if (health == null) continue;

            // Same line-of-sight rule as the Sword: a wall/structure/King
            // between the player and this enemy shields it from the slam.
            if (obstructionLayers.value != 0 &&
                Physics2D.Linecast(origin, health.transform.position, obstructionLayers).collider != null)
                continue;

            health.TakeDamage(damage, fromPlayer: true, isMeleeHit: true);
            hammerEffects.ApplyTo(hit, origin);

            var monster = hit.GetComponentInParent<MonsterAI>();
            if (monster != null) monster.NotifyForcedPlayerEngagement();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 dir = (Application.isPlaying && aim != null) ? aim.AimDirection : Vector2.right;
        Vector2 center = (Vector2)transform.position + dir * reach;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        Gizmos.color = Color.red;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(hitSize.x, hitSize.y, 0.01f));
        Gizmos.matrix = oldMatrix;
    }
}
