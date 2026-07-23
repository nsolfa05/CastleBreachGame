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

    /// <summary>Fired every time health changes (including healing/reset).</summary>
    public event Action<Health> Damaged;

    /// <summary>Fired once when health reaches 0.</summary>
    public event Action<Health> Died;

    private void Awake()
    {
        Current = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || Invulnerable) return;
        Current = Mathf.Max(0f, Current - amount);
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
