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

    /// <summary>Set true while dead/respawning to freeze the player.</summary>
    public bool MovementLocked { get; set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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
        if (keyboard.wKey.isPressed) moveInput.y += 1f;
        if (keyboard.sKey.isPressed) moveInput.y -= 1f;
        if (keyboard.aKey.isPressed) moveInput.x -= 1f;
        if (keyboard.dKey.isPressed) moveInput.x += 1f;
        moveInput = moveInput.normalized; // diagonal movement isn't faster
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = moveInput * moveSpeed;
    }
}
