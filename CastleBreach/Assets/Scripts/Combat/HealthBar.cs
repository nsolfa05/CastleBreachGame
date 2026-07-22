using UnityEngine;

/// <summary>
/// Simple world-space health bar [Placeholder]: a dark background sprite with
/// a colored "fill" child sprite that shrinks toward the left as health drops.
/// Expected hierarchy:
///   HealthBar (this script)
///     Background (Square sprite, scaled to barWidth x ~0.15)
///     Fill       (Square sprite — this script controls its scale/position)
/// </summary>
public class HealthBar : MonoBehaviour
{
    [Tooltip("The Health component this bar displays (usually on the parent object).")]
    [SerializeField] private Health health;

    [Tooltip("The Fill child's Transform.")]
    [SerializeField] private Transform fill;

    [Tooltip("Width of the full bar in world units — match the Background sprite's X scale.")]
    [SerializeField] private float barWidth = 1f;

    [Tooltip("Height of the fill in world units.")]
    [SerializeField] private float fillHeight = 0.12f;

    private void LateUpdate()
    {
        if (health == null || fill == null) return;

        float fraction = health.Max > 0f ? health.Current / health.Max : 0f;
        fill.localScale = new Vector3(barWidth * fraction, fillHeight, 1f);
        // Keep the bar anchored to its left edge while it shrinks
        // (sprites have a center pivot, so we shift the fill left as it scales down).
        fill.localPosition = new Vector3(-barWidth * (1f - fraction) * 0.5f, 0f, 0f);
    }
}
