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
/// placed for the frame. Cursor.visible is also re-asserted every frame
/// here (per GameSettings.HideOsCursor) rather than only once in Awake —
/// a one-time Awake call is fragile: losing/regaining window focus,
/// alt-tabbing, or another script's OnDisable calling Cursor.visible =
/// true can all bring the OS arrow back with nothing to hide it again
/// until the next time this component happens to re-enable.
///
/// GameSettings.HideOsCursor (Settings' "Hide OS Cursor" toggle) picks
/// which pointer is real: on (default), the OS arrow is hidden and this
/// sprite tracks it; off, the OS arrow shows instead and this sprite
/// hides — never both, since one drawn on top of the other looks like a
/// bug. See CursorPresenceCheck for what catches a scene that's missing
/// this component entirely while the toggle expects it hidden.
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
    private int currentSkinIndex;

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
    /// Switches the visible sprite, its hotspot pivot, and its base size,
    /// and persists the choice via GameSettings — called on startup and by
    /// Settings' skin picker. Out-of-range/empty-list indexes are clamped
    /// rather than erroring, since a skin removed from the list shouldn't
    /// crash a save that still references its old index.
    /// </summary>
    public void ApplySkin(int index)
    {
        if (skins.Count == 0) return;

        index = Mathf.Clamp(index, 0, skins.Count - 1);
        currentSkinIndex = index;
        GameSettings.CursorSkinIndex = index;

        CursorSkin skin = skins[index];
        if (image != null) image.sprite = skin.sprite;
        if (rectTransform != null) rectTransform.pivot = skin.pivot;

        ApplyScale(GameSettings.CursorScale);
    }

    /// <summary>
    /// Scales the CURRENT skin's own Base Size and persists the choice —
    /// called on skin switch and by Settings' Cursor Size slider. Sizing
    /// off the active skin's Base Size (not a fixed number) means each
    /// skin keeps its own correct aspect ratio at any scale instead of
    /// every skin sharing one Width/Height that only fits one of them.
    /// </summary>
    public void ApplyScale(float scale)
    {
        GameSettings.CursorScale = scale;

        if (skins.Count == 0 || rectTransform == null) return;

        rectTransform.sizeDelta = skins[currentSkinIndex].baseSize * scale;
    }

    private void LateUpdate()
    {
        bool hideOs = GameSettings.HideOsCursor;
        Cursor.visible = !hideOs;
        Cursor.lockState = CursorLockMode.None;

        // Never show both at once: when the player turns the toggle off,
        // the OS arrow takes over and this sprite steps aside instead of
        // sitting on top of it.
        if (image != null) image.enabled = hideOs;

        var mouse = Mouse.current;
        if (mouse == null) return;

        rectTransform.position = mouse.position.ReadValue();
    }
}
