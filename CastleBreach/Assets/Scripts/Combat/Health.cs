using System;
using UnityEngine;

/// <summary>
/// Shared health component used by the player, the King, monsters and structures.
/// Other scripts react to damage/death by subscribing to the events.
/// </summary>
public class Health : MonoBehaviour
{
    [Tooltip("Maximum (and starting) health. Player 20, King 100, Zombie 10, Archer Tower 50.")]
    [SerializeField] private float maxHealth = 10f;

    public float Max => maxHealth;
    public float Current { get; private set; }
    public bool IsDead => Current <= 0f;

    /// <summary>While true, TakeDamage does nothing (Skeleton bone pile).</summary>
    public bool Invulnerable { get; set; }

    /// <summary>Time.time of the most recent successful TakeDamage call, for anything that needs "was I hit recently" (e.g. MonsterAI's recent-combat check). Negative infinity until ever hit.</summary>
    public float LastDamageTime { get; private set; } = float.NegativeInfinity;

    /// <summary>Whether the most recent hit came from the player specifically.</summary>
    public bool LastDamageFromPlayer { get; private set; }

    /// <summary>Fired every time health changes (including healing/reset).</summary>
    public event Action<Health> Damaged;

    /// <summary>Fired once when health reaches 0.</summary>
    public event Action<Health> Died;

    private void Awake()
    {
        Current = maxHealth;
    }

    public void TakeDamage(float amount, bool fromPlayer = false)
    {
        if (IsDead || Invulnerable) return;
        Current = Mathf.Max(0f, Current - amount);
        LastDamageTime = Time.time;
        LastDamageFromPlayer = fromPlayer;
        Damaged?.Invoke(this);
        if (Current <= 0f)
            Died?.Invoke(this);
    }

    /// <summary>Used by the respawn system; later also by healing effects.</summary>
    public void ResetToFull()
    {
        Current = maxHealth;
        Damaged?.Invoke(this);
    }

    /// <summary>Change max health at runtime — used by MonsterAI to apply a
    /// MonsterDefinition's stats to the generic Monster prefab.</summary>
    public void SetMax(float newMax, bool refill)
    {
        maxHealth = Mathf.Max(1f, newMax);
        Current = refill ? maxHealth : Mathf.Min(Current, maxHealth);
        Damaged?.Invoke(this);
    }
}
