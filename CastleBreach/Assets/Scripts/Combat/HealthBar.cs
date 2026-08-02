using UnityEngine;

/// <summary>
/// Simple world-space health bar [Placeholder]: a dark background sprite with
/// a colored "fill" child sprite that shrinks toward the left as health drops.
///
/// Sizing is driven ENTIRELY by this script's Bar Width / Bar Height fields —
/// it scales BOTH the background and the fill to match, every frame. Don't set
/// the Background's or Fill's own Transform scale by hand; those get overwritten.
/// To resize a bar, change Bar Width / Bar Height here and nowhere else.
///
/// Expected hierarchy:
///   HealthBar (this script)
///     Background (Square sprite — script sizes it to barWidth x barHeight)
///     Fill       (Square sprite — script sizes/positions it)
///
/// Hide Until Damaged (Guide 11c): off = always visible (the original
/// behavior). On = invisible until the entity actually takes damage, then
/// shows for Visible After Damage Seconds before hiding again — cuts the
/// clutter of every full-health monster/tower showing a bar. Reads
/// Health.LastDamageTime directly rather than the Damaged event, since
/// Damaged also fires from ResetToFull/SetMax (e.g. a monster's spawn-time
/// stat setup) — LastDamageTime only moves on a real TakeDamage call, so a
/// freshly spawned full-health monster never flashes its bar for no reason.
/// </summary>
public class HealthBar : MonoBehaviour
{
    [Tooltip("The Health component this bar displays (usually on the parent object).")]
    [SerializeField] private Health health;

    [Tooltip("The Background child's Transform (the dark backing). Optional but recommended.")]
    [SerializeField] private Transform background;

    [Tooltip("The Fill child's Transform (the colored bar).")]
    [SerializeField] private Transform fill;

    [Tooltip("Full width of the bar in world units. This is the ONE place to set the bar's width.")]
    [SerializeField] private float barWidth = 1f;

    [Tooltip("Height of the bar in world units. This is the ONE place to set the bar's height.")]
    [SerializeField] private float barHeight = 0.15f;

    [Header("Hide until damaged (Guide 11c)")]
    [Tooltip("When on, this bar is invisible until Health takes damage, then shows for Visible After Damage Seconds before hiding again. Off = always visible (the original behavior).")]
    [SerializeField] private bool hideUntilDamaged = false;

    [Tooltip("How long (seconds) the bar stays visible after the most recent hit. Only used when Hide Until Damaged is on.")]
    [SerializeField] private float visibleAfterDamageSeconds = 3f;

    private SpriteRenderer backgroundRenderer;
    private SpriteRenderer fillRenderer;

    private void Awake()
    {
        if (background != null) backgroundRenderer = background.GetComponent<SpriteRenderer>();
        if (fill != null) fillRenderer = fill.GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (hideUntilDamaged)
        {
            bool visible = health != null && !health.IsDead &&
                           Time.time - health.LastDamageTime <= visibleAfterDamageSeconds;
            SetVisible(visible);
            if (!visible) return; // no point updating scale/position while hidden
        }

        // Background always spans the full bar size.
        if (background != null)
            background.localScale = new Vector3(barWidth, barHeight, 1f);

        if (health == null || fill == null) return;

        float fraction = health.Max > 0f ? Mathf.Clamp01(health.Current / health.Max) : 0f;

        // Fill is full height, and its width tracks the health fraction.
        fill.localScale = new Vector3(barWidth * fraction, barHeight, 1f);

        // Anchor the fill to the bar's left edge as it shrinks (sprites pivot at
        // their center, so shift left by half of the width that was removed).
        fill.localPosition = new Vector3(-barWidth * (1f - fraction) * 0.5f, 0f, 0f);
    }

    private void SetVisible(bool visible)
    {
        if (backgroundRenderer != null) backgroundRenderer.enabled = visible;
        if (fillRenderer != null) fillRenderer.enabled = visible;
    }
}
