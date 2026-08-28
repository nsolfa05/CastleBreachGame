using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// WASD free-roam movement (design doc §7.2). Input is read in Update,
/// physics movement is applied in FixedUpdate via the Rigidbody2D so the
/// player collides properly with walls and structures.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Tooltip("Movement speed in tiles per second (design doc: 5).")]
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private KnockbackReceiver knockback; // may be null if the prefab has no receiver

    /// <summary>Set true while dead/respawning to freeze the player.</summary>
    public bool MovementLocked { get; set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        knockback = GetComponent<KnockbackReceiver>();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || MovementLocked)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = Vector2.zero;
        if (KeyBindings.IsPressed(KeyBindings.Action.MoveUp)) moveInput.y += 1f;
        if (KeyBindings.IsPressed(KeyBindings.Action.MoveDown)) moveInput.y -= 1f;
        if (KeyBindings.IsPressed(KeyBindings.Action.MoveLeft)) moveInput.x -= 1f;
        if (KeyBindings.IsPressed(KeyBindings.Action.MoveRight)) moveInput.x += 1f;
        moveInput = moveInput.normalized; // diagonal movement isn't faster
    }

    private void FixedUpdate()
    {
        // While being knocked back or stunned, the KnockbackReceiver owns the
        // body — don't fight it (see KnockbackReceiver). Control hands back the
        // instant the shove/stun ends.
        if (knockback != null && knockback.ControlSuppressed) return;

        rb.linearVelocity = moveInput * moveSpeed;
    }
}
