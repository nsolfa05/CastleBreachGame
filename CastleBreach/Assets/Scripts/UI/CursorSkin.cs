using UnityEngine;

/// <summary>
/// One selectable custom-cursor look: a sprite plus its own hotspot pivot.
/// Pivot travels with the sprite rather than being set once globally,
/// because different shapes need different hotspots — a circle's tip is
/// its center, a dagger's is a corner (see Guide 13e). Plain serialized
/// list on CustomCursor, so adding a new skin is just adding a list entry,
/// no code change.
/// </summary>
[System.Serializable]
public class CursorSkin
{
    public string displayName = "Cursor";
    public Sprite sprite;

    [Tooltip("Where this sprite's own 'point' is, as a 0-1 fraction of its Rect — see Guide 13e for how to compute this.")]
    public Vector2 pivot = new Vector2(0.5f, 0.5f);
}
