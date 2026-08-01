using UnityEngine;

/// <summary>
/// The receiving side of a hit's physical reaction: makes an entity (the player
/// or a monster) able to be knocked back and/or stunned. Sits alongside Health
/// on anything that should react to being hit. The attacker side describes WHAT a
/// hit does (see <see cref="HitEffects"/>); this actually shoves/freezes the body.
///
/// The trick that makes this play nicely with the existing movers: while a
/// knockback or stun is active, THIS component owns the Rigidbody2D's velocity.
/// The entity's own mover (PlayerMovement / MonsterAI) checks
/// <see cref="ControlSuppressed"/> and skips its own velocity write, so the two
/// never fight over the body. Because the mover always yields when suppressed,
/// the undefined FixedUpdate order between components on the same object doesn't
/// matter — only this one writes velocity during a knockback/stun, and control
/// hands straight back the instant it expires. That's cleaner than a physics
/// impulse, which the mover would overwrite on the very next FixedUpdate.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class KnockbackReceiver : MonoBehaviour
{
    [Tooltip("Heavier = knocked back LESS by the same hit (knockback speed scales by 1/weight). Matches the design-doc tile weight: most monsters 2, Cyclops & player 6. MonsterAI copies each monster's Tile Weight in here at spawn, so in practice you only set this by hand on the Player.")]
    [SerializeField] private float weight = 2f;

    [Tooltip("Flat 0..1 reduction to ALL knockback taken (0 = full knockback, 1 = immovable), independent of weight — for an entity that should specifically resist being shoved without being made heavy for other purposes.")]
    [Range(0f, 1f)][SerializeField] private float knockbackResistance = 0f;

    [Tooltip("Seconds a knockback shove lasts as it decays to a stop. Short on purpose — it's a shove, not a launch. The stun (if the attack also stuns) is timed separately and takes over once the shove ends.")]
    [SerializeField] private float knockbackDuration = 0.18f;

    [Tooltip("Global multiplier on knockback distance — tune the overall feel here once instead of editing every attack's Strength.")]
    [SerializeField] private float knockbackScale = 1f;

    private Rigidbody2D rb;
    private Vector2 knockbackVelocity;
    private float knockbackEnd;
    private float stunnedUntil;

    /// <summary>True while a knockback or stun is currently driving/holding this
    /// body. The entity's mover MUST skip its own velocity write while this is
    /// true (see class summary), or the two will fight over the Rigidbody.</summary>
    public bool ControlSuppressed => Time.time < knockbackEnd || Time.time < stunnedUntil;

    /// <summary>True while stunned — used to also block ATTACKING, not just movement.</summary>
    public bool IsStunned => Time.time < stunnedUntil;

    private void Awake() => rb = GetComponent<Rigidbody2D>();

    /// <summary>Copy in a spawn-time weight — MonsterAI passes the monster's Tile Weight so heavy types resist knockback without hand-wiring each prefab.</summary>
    public void SetWeight(float newWeight) => weight = Mathf.Max(0.1f, newWeight);

    /// <summary>Shove this body away along <paramref name="direction"/>, at a speed
    /// scaled down by weight and resistance. Ignored if strength or direction is zero.</summary>
    public void ApplyKnockback(Vector2 direction, float strength)
    {
        if (strength <= 0f || direction.sqrMagnitude < 0.0001f) return;

        float speed = strength * knockbackScale * (1f - knockbackResistance) / Mathf.Max(0.1f, weight);
        if (speed <= 0f) return;

        knockbackVelocity = direction.normalized * speed;
        knockbackEnd = Time.time + knockbackDuration;
    }

    /// <summary>Freeze this body in place for <paramref name="duration"/> seconds
    /// (extends, never shortens, any stun already running).</summary>
    public void ApplyStun(float duration)
    {
        if (duration <= 0f) return;
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration);
    }

    private void FixedUpdate()
    {
        if (Time.time < knockbackEnd)
        {
            // Linear decay of the shove to zero over its duration — a quick,
            // readable push rather than a slide that keeps drifting.
            float remaining = knockbackDuration > 0f ? (knockbackEnd - Time.time) / knockbackDuration : 0f;
            rb.linearVelocity = knockbackVelocity * remaining;
        }
        else if (Time.time < stunnedUntil)
        {
            rb.linearVelocity = Vector2.zero; // held in place after the shove ends
        }
        // else: not suppressed — leave the body entirely to its normal mover.
    }
}
