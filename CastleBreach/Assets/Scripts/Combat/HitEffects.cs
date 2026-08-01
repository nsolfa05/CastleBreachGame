using UnityEngine;

/// <summary>
/// The non-damage part of an attack: optional knockback + optional stun. Embedded
/// as an Inspector foldout in anything that deals hits — the player's weapons, a
/// MonsterDefinition, a tower — so each attack tunes its own. Everything defaults
/// OFF, per the design brief: knockback/stun are a capability every attacker CAN
/// switch on, not something that happens automatically.
///
/// Applied via <see cref="ApplyTo"/>, with knockback directed radially AWAY from
/// a source point — so the one struct covers a melee shove (source = the
/// attacker), a catapult's splash pushing outward (source = the blast centre),
/// and a future head-on tower knock (source = the tower). The target only reacts
/// if it has a <see cref="KnockbackReceiver"/>; the King and structures don't, so
/// they simply ignore it, which is exactly what we want.
///
/// This is the first piece of the Guide 11 combat framework: damage still travels
/// through Health.TakeDamage as before; this rides alongside it so damage and its
/// physical reaction stay one conceptual "hit" without Health needing to know
/// anything about knockback.
/// </summary>
[System.Serializable]
public struct HitEffects
{
    [Tooltip("When on, this attack shoves the target back — away from the hit's source point. Off by default.")]
    public bool knockbackEnabled;

    [Tooltip("How hard the shove is BEFORE the target's own weight/resistance reduce it. Bigger = further. See KnockbackReceiver for how weight scales it down.")]
    public float knockbackStrength;

    [Tooltip("When on, this attack briefly freezes the target in place — it can't move OR attack for the duration. Off by default.")]
    public bool stunEnabled;

    [Tooltip("Seconds the stun lasts.")]
    public float stunDuration;

    /// <summary>
    /// Apply this hit's knockback/stun to <paramref name="target"/> (found via its
    /// KnockbackReceiver, searched up the parents so a child collider still
    /// resolves to the body). <paramref name="sourcePosition"/> is where the
    /// knockback pushes AWAY from. No-ops if neither effect is enabled or the
    /// target has no receiver.
    /// </summary>
    public void ApplyTo(Component target, Vector2 sourcePosition)
    {
        if (target == null || (!knockbackEnabled && !stunEnabled)) return;

        var receiver = target.GetComponentInParent<KnockbackReceiver>();
        if (receiver == null) return;

        if (knockbackEnabled)
            receiver.ApplyKnockback((Vector2)receiver.transform.position - sourcePosition, knockbackStrength);
        if (stunEnabled)
            receiver.ApplyStun(stunDuration);
    }
}
