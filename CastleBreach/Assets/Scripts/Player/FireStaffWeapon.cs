using UnityEngine;

/// <summary>
/// The Fire Staff — charge-and-release ranged weapon (Guide 11b). Hold Space
/// to charge, release to launch a bolt that flies straight (StraightProjectile,
/// same flight behavior as the Bow) and, wherever it ends up — a monster it
/// hit, or just the point Range tiles out if nothing was in the way — leaves a
/// BurnZone that ticks damage over time. All the staff's damage comes from the
/// burn; there's no separate direct-hit hit, so a bolt that lands square on a
/// monster and one that lands on empty ground right next to it behave the same
/// once the fire catches.
/// </summary>
public class FireStaffWeapon : ChargedWeapon
{
    [Header("Fire Staff (Guide 11b)")]
    [Tooltip("Max travel distance (tiles) before the bolt lands anyway.")]
    [SerializeField] private float range = 4.5f;
    [SerializeField] private float projectileSpeed = 8f;
    [Tooltip("What the bolt can hit in flight — set this to the Enemy layer.")]
    [SerializeField] private LayerMask hitLayers;

    [Header("Burn")]
    [SerializeField] private float damagePerTick = 2f;
    [SerializeField] private float tickInterval = 1f;
    [SerializeField] private float duration = 6f;
    [Tooltip("Radius (tiles) of the burn zone left behind — \"2 tiles wide\" read as a 1-tile radius / 2-tile diameter patch.")]
    [SerializeField] private float burnRadius = 1f;

    [Header("Visuals [Placeholder]")]
    [SerializeField] private Sprite boltSprite;
    [SerializeField] private Color boltColor = new Color(1f, 0.5f, 0.1f);
    [SerializeField] private Vector2 boltSize = new Vector2(0.4f, 0.4f);
    [SerializeField] private Sprite burnSprite;
    [SerializeField] private Color burnColor = new Color(1f, 0.4f, 0.1f, 0.35f);

    protected override Vector2 ChargeBarSize => new Vector2(range, 0.3f);

    protected override void Awake()
    {
        base.Awake();
        // A gate-passing monster (the Goblin) spawns onto the GatePasser layer,
        // not Enemy — without this the bolt would fly straight through it.
        hitLayers = MonsterLayers.IncludeGatePasser(hitLayers);
    }

    protected override void Fire(Vector2 origin, Vector2 direction)
    {
        var go = new GameObject("FireBolt");
        go.transform.position = origin;
        go.transform.localScale = new Vector3(boltSize.x, boltSize.y, 1f);

        if (boltSprite != null)
        {
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = boltSprite;
            renderer.color = boltColor;
            renderer.sortingOrder = 25;
        }

        var projectile = go.AddComponent<StraightProjectile>();
        projectile.Launch(direction, projectileSpeed, range, hitLayers, (point, hitCollider) =>
        {
            // Every landing spot burns — a clean hit and a missed shot that
            // simply ran out of range both leave the same zone behind.
            BurnZone.Spawn(point, burnRadius, damagePerTick, tickInterval, duration,
                hitLayers, burnSprite, burnColor, sortingOrder: 4);
        });
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 dir = (Application.isPlaying && aim != null) ? aim.AimDirection : Vector2.right;
        Gizmos.color = new Color(1f, 0.4f, 0.1f);
        Gizmos.DrawLine(transform.position, (Vector2)transform.position + dir * range);
    }
}
