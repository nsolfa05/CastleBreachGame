using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A ground effect that damages anything with Health standing inside it, once
/// per tick, for its lifetime, then removes itself — fire-and-forget, same
/// spawn convention as ImpactMark (a fresh GameObject, no prefab needed).
/// Built for the Fire Staff's landing burn, but deliberately generic (radius,
/// tick rate, damage, duration, an optional player-damage toggle all passed in
/// at spawn) so a future ground effect — the Oil & Flame tower's flame tiles,
/// say — reuses this instead of new per-attack DoT code.
/// </summary>
public class BurnZone : MonoBehaviour
{
    private float radius;
    private float damagePerTick;
    private float tickInterval;
    private float endTime;
    private float nextTickTime;
    private LayerMask hitLayers;
    private bool hitsPlayer;
    private bool fromPlayer;

    /// <summary>
    /// Create and configure a burn zone at worldPosition. hitLayers decides who
    /// it can even see (pass Enemy [+ GatePasser] to only ever threaten
    /// monsters); hitsPlayer additionally gates whether the Player specifically
    /// takes damage from it, for a future zone that also wants to hit the player
    /// (only meaningful if hitLayers includes the Player layer too). fromPlayer
    /// is recorded on every tick's TakeDamage call so recent-combat aggro
    /// (Health.LastDamageFromPlayer) reacts correctly to it — pass true for a
    /// player-caused zone (the Fire Staff), false for anything else.
    /// </summary>
    public static BurnZone Spawn(Vector2 worldPosition, float radiusTiles, float damagePerTickValue,
                                 float tickIntervalSeconds, float durationSeconds, LayerMask layers,
                                 Sprite visualSprite = null, Color visualColor = default,
                                 int sortingOrder = 4, bool damagesPlayer = false, bool fromPlayer = false)
    {
        var go = new GameObject("BurnZone");
        go.transform.position = worldPosition;

        if (visualSprite != null)
        {
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = visualSprite;
            renderer.color = visualColor;
            renderer.sortingOrder = sortingOrder;
            go.transform.localScale = new Vector3(radiusTiles * 2f, radiusTiles * 2f, 1f);
        }

        var zone = go.AddComponent<BurnZone>();
        zone.radius = radiusTiles;
        zone.damagePerTick = damagePerTickValue;
        zone.tickInterval = Mathf.Max(0.05f, tickIntervalSeconds);
        zone.endTime = Time.time + durationSeconds;
        zone.nextTickTime = Time.time; // first tick lands immediately, not after one full interval
        zone.hitLayers = layers;
        zone.hitsPlayer = damagesPlayer;
        zone.fromPlayer = fromPlayer;
        return zone;
    }

    private void Update()
    {
        if (Time.time >= endTime) { Destroy(gameObject); return; }
        if (Time.time < nextTickTime) return;
        nextTickTime = Time.time + tickInterval;

        var gm = GameManager.Instance;
        var hits = Physics2D.OverlapCircleAll(transform.position, radius, hitLayers);
        var damaged = new HashSet<Health>(); // each Health takes at most one tick's damage per interval, even with multiple colliders
        foreach (var hit in hits)
        {
            var health = hit.GetComponentInParent<Health>();
            if (health == null || !damaged.Add(health)) continue;
            if (gm != null && health == gm.PlayerHealth && !hitsPlayer) continue;
            health.TakeDamage(damagePerTick, fromPlayer); // DoT tick — never counts as a melee hit
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
