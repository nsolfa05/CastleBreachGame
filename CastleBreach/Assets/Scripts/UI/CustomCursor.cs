using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Custom on-screen cursor (§2), replacing the OS pointer, on a Screen
/// Space - Overlay Canvas Image so it renders identically in every scene —
/// same prefab dropped into Title/Settings/Campaign/Game.
///
/// Pinned EXACTLY to the real pointer every frame, with no smoothing or
/// easing, deliberately: aiming and building (PlayerAim,
/// BuildModeController) both read the raw mouse position, so any easing
/// here would draw the cursor somewhere other than where the player is
/// actually aiming or placing a structure — an accuracy bug, not just a
/// look-and-feel one. An earlier version eased toward the pointer at
/// GameSettings.CursorSpeed and had exactly that problem.
///
/// GameSettings.CursorSpeed is therefore unused by the mouse path and
/// reserved for a future gamepad-driven cursor, which has no absolute
/// position — only a stick direction — and would integrate position from
/// stick input each frame at that speed (position += stickInput * speed *
/// dt). Not built yet; no gamepad input exists anywhere in this project.
///
/// LateUpdate, not Update, so nothing else can move the cursor after it's
/// placed for the frame.
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

    private void LateUpdate()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        rectTransform.position = mouse.position.ReadValue();
    }
}
