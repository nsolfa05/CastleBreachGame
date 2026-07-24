using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mouse-aimed sword swing (design doc §7.2): spacebar to attack,
/// hits everything on `hitLayers` inside a box 1 tile ahead of the player
/// and 3 tiles wide, with a 0.5s cooldown between swings.
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Header("Combat (design doc §7.2)")]
    [SerializeField] private float damage = 2f;
    [SerializeField] private float cooldown = 0.5f;

    [Tooltip("How far ahead of the player the center of the swing lands (tiles).")]
    [SerializeField] private float reach = 1f;

    [Tooltip("Width of the swing arc (tiles).")]
    [SerializeField] private float arcWidth = 3f;

    [Tooltip("What the sword can hit — set this to the Enemy layer.")]
    [SerializeField] private LayerMask hitLayers;

    [Header("Visuals [Placeholder]")]
    [Tooltip("Child object rotated to face the mouse; the swing sprite lives under it.")]
    [SerializeField] private Transform aimPivot;

    [Tooltip("Sprite briefly flashed when swinging.")]
    [SerializeField] private SpriteRenderer swingVisual;

    [SerializeField] private float swingFlashSeconds = 0.12f;

    private float nextSwingTime;
    private float swingVisualOffTime;

    public Vector2 AimDirection { get; private set; } = Vector2.right;

    private void Awake()
    {
        if (swingVisual != null)
            swingVisual.enabled = false; // only visible during the swing flash
    }

    private void Update()
    {
        UpdateAim();

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame && Time.time >= nextSwingTime)
            Swing();

        if (swingVisual != null && swingVisual.enabled && Time.time >= swingVisualOffTime)
            swingVisual.enabled = false;
    }

    private void UpdateAim()
    {
        var mouse = Mouse.current;
        var cam = Camera.main;
        if (mouse == null || cam == null) return;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(mouse.position.ReadValue());
        Vector2 toMouse = (Vector2)(mouseWorld - transform.position);
        if (toMouse.sqrMagnitude > 0.001f)
            AimDirection = toMouse.normalized;

        if (aimPivot != null)
        {
            float angle = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg;
            aimPivot.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void Swing()
    {
        nextSwingTime = Time.time + cooldown;

        if (swingVisual != null)
        {
            swingVisual.enabled = true;
            swingVisualOffTime = Time.time + swingFlashSeconds;
        }

        Vector2 center = (Vector2)transform.position + AimDirection * reach;
        float angle = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg;
        var hits = Physics2D.OverlapBoxAll(center, new Vector2(reach, arcWidth), angle, hitLayers);
        foreach (var hit in hits)
        {
            var health = hit.GetComponentInParent<Health>();
            if (health != null)
                health.TakeDamage(damage, fromPlayer: true);
        }
    }

    // Draws the swing hitbox in the Scene view while the player is selected.
    private void OnDrawGizmosSelected()
    {
        Vector2 dir = Application.isPlaying ? AimDirection : Vector2.right;
        Vector2 center = (Vector2)transform.position + dir * reach;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        Gizmos.color = Color.yellow;
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(reach, arcWidth, 0f));
        Gizmos.matrix = Matrix4x4.identity;
    }
}
