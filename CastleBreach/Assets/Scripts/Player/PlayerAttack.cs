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
///
/// The swing FLASH is the same wedge shape too — a LineRenderer traces
/// exactly the outline OnDrawGizmosSelected draws in the Scene view (both
/// read from BuildWedgePoints, so the two can never drift apart), replacing
/// the old flat rectangle sprite placeholder.
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

    [Header("Visuals")]
    [Tooltip("Color of the swing wedge flash.")]
    [SerializeField] private Color swingVisualColor = new Color(1f, 0.95f, 0.4f, 0.9f);

    [Tooltip("Line thickness (tiles) of the swing wedge flash.")]
    [SerializeField] private float swingVisualWidth = 0.06f;

    [Tooltip("How long the swing wedge flash stays visible, in seconds.")]
    [SerializeField] private float swingFlashSeconds = 0.12f;

    private const int WedgeSegments = 16;

    private float nextSwingTime;
    private float swingVisualOffTime;
    private PlayerAim aim;
    private KnockbackReceiver knockback; // may be null; used only to block attacking while stunned
    private LineRenderer swingLine;

    private void Awake()
    {
        swingLine = GetComponent<LineRenderer>();
        if (swingLine == null) swingLine = gameObject.AddComponent<LineRenderer>();
        swingLine.useWorldSpace = true;
        swingLine.loop = false;
        swingLine.positionCount = 0;
        swingLine.widthMultiplier = swingVisualWidth;
        swingLine.startColor = swingVisualColor;
        swingLine.endColor = swingVisualColor;
        swingLine.sortingOrder = 25;
        // Sprites/Default: the same shader every SpriteRenderer in this
        // project already relies on, so it's guaranteed URP-compatible here
        // — LineRenderer's own default material is not (shows up magenta).
        swingLine.material = new Material(Shader.Find("Sprites/Default"));
        swingLine.enabled = false;

        aim = GetComponent<PlayerAim>();
        knockback = GetComponent<KnockbackReceiver>();

        // A gate-passing monster (the Goblin) spawns onto the GatePasser layer,
        // not Enemy — without this the sword's arc would filter it out and it
        // would take no damage. See MonsterLayers.
        hitLayers = MonsterLayers.IncludeGatePasser(hitLayers);
    }

    // Being switched away from mid-flash (or disabled on death) shouldn't
    // leave the wedge flash frozen on screen — WeaponSwitcher only disables
    // this component, it never touches the flash directly.
    private void OnDisable()
    {
        if (swingLine != null) swingLine.enabled = false;
    }

    private void Update()
    {
        // A stunned player can't swing (matches losing movement control).
        bool stunned = knockback != null && knockback.IsStunned;

        var keyboard = Keyboard.current;
        if (!stunned && keyboard != null && keyboard.spaceKey.wasPressedThisFrame && Time.time >= nextSwingTime)
            Swing();

        if (swingLine.enabled && Time.time >= swingVisualOffTime)
            swingLine.enabled = false;
    }

    private void Swing()
    {
        nextSwingTime = Time.time + cooldown;

        Vector2 origin = transform.position;
        Vector2 aimDir = aim != null ? aim.AimDirection : Vector2.right;

        var wedgePoints = BuildWedgePoints(origin, aimDir);
        swingLine.positionCount = wedgePoints.Length;
        swingLine.SetPositions(wedgePoints);
        swingLine.enabled = true;
        swingVisualOffTime = Time.time + swingFlashSeconds;

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

    // Draws the swing's arc (a pie-slice wedge outline) in the Scene view
    // while selected — the exact same points the real swing flash uses.
    private void OnDrawGizmosSelected()
    {
        Vector2 dir = (Application.isPlaying && aim != null) ? aim.AimDirection : Vector2.right;
        var points = BuildWedgePoints(transform.position, dir);

        Gizmos.color = Color.yellow;
        for (int i = 0; i < points.Length - 1; i++)
            Gizmos.DrawLine(points[i], points[i + 1]);
    }

    /// <summary>Origin → near edge → arc → far edge → origin, the wedge outline
    /// shared by the real-time swing flash (LineRenderer) and the Scene-view
    /// gizmo, so they can never draw two different shapes.</summary>
    private Vector3[] BuildWedgePoints(Vector2 origin, Vector2 dir)
    {
        float halfAngleRad = Mathf.Atan2(arcWidth * 0.5f, Mathf.Max(0.01f, reach));
        float baseAngleRad = Mathf.Atan2(dir.y, dir.x);

        var points = new Vector3[WedgeSegments + 3];
        int idx = 0;
        points[idx++] = origin;
        for (int i = 0; i <= WedgeSegments; i++)
        {
            float t = (float)i / WedgeSegments;
            float a = baseAngleRad - halfAngleRad + t * (2f * halfAngleRad);
            points[idx++] = origin + AngleToOffset(a, reach);
        }
        points[idx++] = origin;
        return points;
    }

    private static Vector2 AngleToOffset(float angleRad, float radius) =>
        new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * radius;
}
