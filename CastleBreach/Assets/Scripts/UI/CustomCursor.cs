using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Custom on-screen cursor (§2), replacing the OS pointer, on a Screen
/// Space - Overlay Canvas Image so it renders identically in every scene —
/// same prefab dropped into Title/Settings/Campaign/Game. Eases toward the
/// raw mouse position at GameSettings.CursorSpeed: high values feel
/// effectively instant, low values give a visible trailing effect.
///
/// Mouse gives an absolute screen position every frame, so "speed" here
/// means how fast the VISUAL catches up to it. This is deliberately
/// groundwork for a future gamepad-driven cursor, which has no absolute
/// position — only a stick direction — and would instead integrate
/// position directly from stick input each frame at this same speed value
/// (position += stickInput * speed * dt, replacing the Lerp below). Not
/// built yet since no gamepad input exists anywhere in this project.
///
/// IMPORTANT (Editor setup, not code): the Image this sits on must have
/// Raycast Target OFF. It's always positioned exactly under the pointer,
/// so with Raycast Target on it would intercept every UI click meant for
/// whatever's underneath it — every button would stop working.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CustomCursor : MonoBehaviour
{
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnDisable()
    {
        Cursor.visible = true;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 target = mouse.position.ReadValue();
        float t = 1f - Mathf.Exp(-GameSettings.CursorSpeed * Time.unscaledDeltaTime);
        rectTransform.position = Vector2.Lerp(rectTransform.position, target, t);
    }
}
