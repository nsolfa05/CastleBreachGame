using System.Collections;
using UnityEngine;

/// <summary>
/// Design doc §10.1: on reaching 0 HP the player disappears and respawns at
/// their starting position after a tunable delay, losing some gold on the way
/// (Guide 11c) — the mode is a dropdown so it's easy to try LoseAll vs a
/// percentage vs a flat amount without rewriting anything. (The "keep or lose
/// upgrades on death" toggle arrives together with the upgrade system,
/// post-slice.)
///
/// If a DeathEffect is attached, its red-tint + particle burst plays first —
/// movement/collision/weapons lock immediately, but the tinted BODY stays
/// visible for however long DeathEffect.Play() reports back (Body Lifetime
/// Seconds) before actually being hidden. That time counts toward Respawn
/// Delay, not on top of it — set Respawn Delay at least as long as Body
/// Lifetime Seconds or the corpse effectively floors how soon you can respawn.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerRespawn : MonoBehaviour
{
    private enum GoldLossMode { LoseAll, LosePercentage, LoseFixedAmount }

    [Header("Respawn")]
    [Tooltip("Seconds until respawn (design doc §10.1 — tunable).")]
    [SerializeField] private float respawnDelay = 3f;

    [Header("Gold loss on death (Guide 11c)")]
    [Tooltip("How much of the player's gold is lost on death. Lose All: everything. Lose Percentage: a fraction of current gold, the rest is kept. Lose Fixed Amount: always the same flat number, floored at 0.")]
    [SerializeField] private GoldLossMode goldLossMode = GoldLossMode.LoseAll;

    [Tooltip("Used when Gold Loss Mode is Lose Percentage — 0.5 = lose half your current gold, keep the rest.")]
    [Range(0f, 1f)]
    [SerializeField] private float goldLossPercentage = 0.5f;

    [Tooltip("Used when Gold Loss Mode is Lose Fixed Amount — always lose exactly this much (floored at 0 gold), regardless of how much you're carrying.")]
    [SerializeField] private int goldLossFixedAmount = 50;

    private Health health;
    private Vector3 spawnPosition;

    private void Awake()
    {
        health = GetComponent<Health>();
        spawnPosition = transform.position;
        health.Died += OnDied;
    }

    private void OnDied(Health _)
    {
        ApplyGoldLoss();
        StartCoroutine(RespawnRoutine());
    }

    private void ApplyGoldLoss()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        int amount = goldLossMode switch
        {
            GoldLossMode.LoseAll => gm.Gold,
            GoldLossMode.LosePercentage => Mathf.RoundToInt(gm.Gold * goldLossPercentage),
            GoldLossMode.LoseFixedAmount => goldLossFixedAmount,
            _ => 0,
        };
        gm.RemoveGold(amount);
    }

    private IEnumerator RespawnRoutine()
    {
        var deathEffect = GetComponent<DeathEffect>();
        float lingerSeconds = deathEffect != null ? deathEffect.Play() : 0f;

        // Lock movement/collision/weapons right away, but leave the sprites
        // showing so DeathEffect's tinted corpse is actually visible.
        LockControl(true);
        if (lingerSeconds > 0f) yield return new WaitForSeconds(lingerSeconds);
        SetBodyVisible(false);

        float remaining = respawnDelay - lingerSeconds;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        transform.position = spawnPosition;
        health.ResetToFull();
        SetBodyVisible(true);
        LockControl(false);
    }

    private void LockControl(bool locked)
    {
        foreach (var collider in GetComponentsInChildren<Collider2D>(true))
            collider.enabled = !locked;

        var movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.MovementLocked = locked;

        var weapons = GetComponent<WeaponSwitcher>();
        if (weapons != null) weapons.SetCombatEnabled(!locked);
    }

    private void SetBodyVisible(bool visible)
    {
        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            // Combat FX (a weapon's charge bar) manages its own visibility —
            // forcing it ON here would show whatever on/off state it was
            // frozen in the instant the player died (e.g. a charge bar
            // stuck mid-fill). Forcing it OFF on death is still fine and
            // instant either way, so only showing again skips it.
            if (visible && renderer.GetComponent<CombatFxVisual>() != null) continue;
            renderer.enabled = visible;
        }
    }
}
