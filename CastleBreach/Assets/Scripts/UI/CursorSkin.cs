using UnityEngine;

/// <summary>
/// One selectable custom-cursor look: a sprite, its own hotspot pivot, and
/// its own base display size. Pivot and size both travel with the sprite
/// rather than being set once globally, because different art needs both
/// set differently — a circle's hotspot is its center and a dagger's is a
/// corner (see Guide 13e), and a UI Image stretches to fill whatever
/// Width/Height it's given, so a skin with the wrong size for its sprite's
/// aspect ratio would visibly distort. Plain serialized list on
/// CustomCursor, so adding a new skin is just adding a list entry, no
/// code change.
/// </summary>
[System.Serializable]
public class CursorSkin
{
    public string displayName = "Cursor";
    public Sprite sprite;

    [Tooltip("Where this sprite's own 'point' is, as a 0-1 fraction of its Rect — see Guide 13e for how to compute this.")]
    public Vector2 pivot = new Vector2(0.5f, 0.5f);

    [Tooltip("This skin's own display size in pixels, BEFORE the player's Cursor Size setting scales it. Match your sprite's aspect ratio or it'll stretch.")]
    public Vector2 baseSize = new Vector2(32f, 32f);
}
