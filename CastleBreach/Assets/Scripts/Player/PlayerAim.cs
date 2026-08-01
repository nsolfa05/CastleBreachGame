using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Single source of "which way is the player aiming" (mouse direction from the
/// player), read by every weapon (Sword, Bow, Hammer, Fire Staff) instead of
/// each one tracking the mouse independently — extracted out of the old
/// PlayerAttack now that Guide 11b gives the player four weapons that all need
/// the exact same aim vector every frame. Also optionally rotates a visual pivot
/// Transform to face the aim direction, for whichever weapon's swing/charge
/// visuals live under one.
/// </summary>
public class PlayerAim : MonoBehaviour
{
    [Tooltip("Child object rotated to face the mouse — the sword's swing sprite (and other weapons' visuals, if parented here) lives under it. Optional.")]
    [SerializeField] private Transform aimPivot;

    public Vector2 AimDirection { get; private set; } = Vector2.right;

    private void Update()
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
}
