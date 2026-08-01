using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The Sword — the player's starting weapon (Guide 11b), managed as weapon
/// slot 0 by WeaponSwitcher. Mouse-aimed melee swing: spacebar to attack,
/// hits everything on `hitLayers` inside an ARC in front of the player —
/// roughly Arc Width tiles wide at Reach tiles out — with a Cooldown between
/// swings and no wind-up (unlike the other three weapons).
///
/// The arc is a true angular wedge centered on the player (not a rectangle
/// offset ahead, which is what this used to be): every candidate within
/// Reach of the player is tested against the swing's half-angle, so it reads
/// as a sweeping cut around the wielder rather than a static box. Reach and
/// Arc Width stay the two Inspector knobs either way — the half-angle is
/// just derived from them (atan2(halfWidth, reach)) so tuning them still
/// feels like "how wide / how far", not "how many degrees".
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Header("Combat (design doc §7.2)")]
    [SerializeField] private float damage = 2f;
    [SerializeField] private float cooldown = 0.5f;

    [Tooltip("How far the swing reaches (tiles) — the arc's radius.")]
    [SerializeField] private float reach = 1f;

    [Tooltip("Width of the swing arc (tiles) at Reach distance — sets the arc's angular width (wider = a bigger sweep), not a straight-line box width anymore.")]
    [SerializeField] private float arcWidth = 3f;

    [Tooltip("What the sword can hit — set this to the Enemy layer.")]
    [SerializeField] private LayerMask hitLayers;

    [Tooltip("Layers that BLOCK the swing — set to Structure + King (walls/gates/towers sit on Structure). An enemy with one of these on the straight line between it and the player is spared: the swing only bites on the player's side of a wall, never through it.")]
    [SerializeField] private LayerMask obstructionLayers;

    [Tooltip("Knockback / stun this sword applies on hit — the first user of the Guide 11 combat framework. Off by default; turn on Knockback and/or Stun and set the numbers to give the sword its shove + brief freeze.")]
    [SerializeField] private HitEffects swordEffects;

    [Header("Visuals [Placeholder]")]
    [Tooltip("Sprite briefly flashed when swinging.")]
    [SerializeField] private SpriteRenderer swingVisual;

    [SerializeField] private float swingFlashSeconds = 0.12f;

    private float nextSwingTime;
    private float swingVisualOffTime;
    private PlayerAim aim;
    private KnockbackReceiver knockback; // may be null; used only to block attacking while stunned

    private void Awake()
    {
        if (swingVisual != null)
            swingVisual.enabled = false; // only visible during the swing flash

        aim = GetComponent<PlayerAim>();
        knockback = GetComponent<KnockbackReceiver>();

        // A gate-passing monster (the Goblin) spawns onto the GatePasser layer,
        // not Enemy — without this the sword's arc would filter it out and it
        // would take no damage. See MonsterLayers.
        hitLayers = MonsterLayers.IncludeGatePasser(hitLayers);
    }

    private void Update()
    {
        // A stunned player can't swing (matches losing movement control).
        bool stunned = knockback != null && knockback.IsStunned;

        var keyboard = Keyboard.current;
        if (!stunned && keyboard != null && keyboard.spaceKey.wasPressedThisFrame && Time.time >= nextSwingTime)
            Swing();

        if (swingVisual != null && swingVisual.enabled && Time.time >= swingVisualOffTime)
            swingVisual.enabled = false;
    }

    private void Swing()
    {
        nextSwingTime = Time.time + cooldown;

        if (swingVisual != null)
        {
            swingVisual.enabled = true;
            swingVisualOffTime = Time.time + swingFlashSeconds;
        }

        Vector2 origin = transform.position;
        Vector2 aimDir = aim != null ? aim.AimDirection : Vector2.right;
        float halfAngleRad = Mathf.Atan2(arcWidth * 0.5f, Mathf.Max(0.01f, reach));

        var hits = Physics2D.OverlapCircleAll(origin, reach, hitLayers);
        foreach (var hit in hits)
        {
            var health = hit.GetComponentInParent<Health>();
            if (health == null) continue;

            Vector2 toTarget = (Vector2)health.transform.position - origin;
            if (toTarget.sqrMagnitude < 0.0001f) continue; // exactly on top of the player — nothing meaningful to aim at

            // Inside the swing's cone? Angle between where we're aiming and where
            // the target actually is, compared against the arc's half-width.
            float angleToTarget = Vector2.Angle(aimDir, toTarget) * Mathf.Deg2Rad;
            if (angleToTarget > halfAngleRad) continue;

            // Wall block (line-of-sight): if a wall/structure/King sits on the
            // straight line from the player to this enemy, the swing doesn't reach
            // it — the arc only bites on the player's side of the obstacle, never
            // through it. (A cheaper stand-in for a literal arc that stops on
            // contact; a real expanding-hitbox version is noted for later.)
            if (obstructionLayers.value != 0 &&
                Physics2D.Linecast(origin, health.transform.position, obstructionLayers).collider != null)
                continue;

            health.TakeDamage(damage, fromPlayer: true);
            swordEffects.ApplyTo(hit, origin); // knockback pushes the enemy away from the player
        }
    }

    // Draws the swing's arc (a pie-slice wedge) in the Scene view while selected.
    private void OnDrawGizmosSelected()
    {
        Vector2 dir = (Application.isPlaying && aim != null) ? aim.AimDirection : Vector2.right;
        Vector3 origin = transform.position;
        float halfAngleRad = Mathf.Atan2(arcWidth * 0.5f, Mathf.Max(0.01f, reach));
        float baseAngleRad = Mathf.Atan2(dir.y, dir.x);

        Gizmos.color = Color.yellow;
        const int segments = 16;
        Vector3 prevPoint = origin + AngleToOffset(baseAngleRad - halfAngleRad, reach);
        Gizmos.DrawLine(origin, prevPoint); // near edge of the wedge
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            float a = baseAngleRad - halfAngleRad + t * (2f * halfAngleRad);
            Vector3 point = origin + AngleToOffset(a, reach);
            Gizmos.DrawLine(prevPoint, point); // the arc itself
            prevPoint = point;
        }
        Gizmos.DrawLine(origin, prevPoint); // far edge of the wedge
    }

    private static Vector3 AngleToOffset(float angleRad, float radius) =>
        new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f) * radius;
}
