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
        if (IsDead) return;
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
}
