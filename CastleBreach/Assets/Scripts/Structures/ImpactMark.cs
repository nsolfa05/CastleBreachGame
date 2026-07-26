using UnityEngine;

/// <summary>
/// Pure visuals for a projectile's impact point [Placeholder] — a simple
/// circle marking where it landed, shown briefly then gone. Self-contained
/// and fire-and-forget (unlike TelegraphedAreaAttack's reusable boxes): the
/// projectile that spawns it is destroyed the same frame, so this owns and
/// cleans up its own short-lived GameObject instead of relying on a parent.
/// Used by Projectile on impact — sized to Splash Radius so splash weapons
/// (the Catapult) show exactly where the blast landed and how far it reached.
/// </summary>
public class ImpactMark : MonoBehaviour
{
    private float hideTime;

    /// <summary>Creates and configures a new impact mark at worldPosition. No-op if sprite is unassigned.</summary>
    public static void Spawn(Vector2 worldPosition, Sprite sprite, Color color, float diameter, float visibleSeconds, int sortingOrder)
    {
        if (sprite == null || visibleSeconds <= 0f) return;

        var go = new GameObject("ImpactMark");
        go.transform.position = worldPosition;
        go.transform.localScale = new Vector3(diameter, diameter, 1f);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        var mark = go.AddComponent<ImpactMark>();
        mark.hideTime = Time.time + visibleSeconds;
    }

    private void Update()
    {
        if (Time.time >= hideTime)
            Destroy(gameObject);
    }
}
