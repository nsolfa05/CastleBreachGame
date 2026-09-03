using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
/// placed for the frame. Cursor.visible is also re-asserted false every
/// frame here rather than only once in Awake — a one-time Awake call is
/// fragile: losing/regaining window focus, alt-tabbing, or another
/// script's OnDisable calling Cursor.visible = true can all bring the OS
/// arrow back with nothing to hide it again until the next time this
/// component happens to re-enable.
///
/// IMPORTANT (Editor setup, not code): the Image this sits on must have
/// Raycast Target OFF. It's always positioned exactly under the pointer,
/// so with Raycast Target on it would intercept every UI click meant for
/// whatever's underneath it — every button would stop working.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CustomCursor : MonoBehaviour
{
    [Tooltip("Selectable cursor looks — see Settings' cursor skin picker. Index 0 is the default.")]
    [SerializeField] private List<CursorSkin> skins = new List<CursorSkin>();

    [SerializeField] private Image image;

    private RectTransform rectTransform;

    public IReadOnlyList<CursorSkin> Skins => skins;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (image == null) image = GetComponent<Image>();

        ApplySkin(GameSettings.CursorSkinIndex);
    }

    private void OnDisable()
    {
        Cursor.visible = true;
    }

    /// <summary>
    /// Switches the visible sprite and its hotspot pivot, and persists the
    /// choice via GameSettings — called on startup and by Settings' skin
    /// picker. Out-of-range/empty-list indexes are clamped rather than
    /// erroring, since a skin removed from the list shouldn't crash a
    /// save that still references its old index.
    /// </summary>
    public void ApplySkin(int index)
    {
        if (skins.Count == 0) return;

        index = Mathf.Clamp(index, 0, skins.Count - 1);
        GameSettings.CursorSkinIndex = index;

        CursorSkin skin = skins[index];
        if (image != null) image.sprite = skin.sprite;
        if (rectTransform != null) rectTransform.pivot = skin.pivot;
    }

    private void LateUpdate()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;

        var mouse = Mouse.current;
        if (mouse == null) return;

        rectTransform.position = mouse.position.ReadValue();
    }
}
