using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shared "hold Space to charge, release to fire" state machine for the Bow,
/// Hammer, and Fire Staff — the three weapons with a wind-up, unlike the
/// Sword's instant swing. Subclasses only implement what happens once actually
/// fired (<see cref="Fire"/>) and the charge bar's on-screen size; this owns
/// reading Space, tracking charge progress, gating on stun, and driving the
/// shared <see cref="ChargeIndicator"/>.
///
/// Release timing rule (a judgment call — flagged in Guide 11b): releasing
/// BEFORE Wind Up Time completes cancels the attempt with no effect and no
/// cooldown penalty; releasing at or after full charge fires immediately.
/// Holding longer than the wind-up costs nothing — only the release moment
/// matters once you're past it, so there's no need to release at an exact
/// instant.
/// </summary>
public abstract class ChargedWeapon : MonoBehaviour
{
    [Tooltip("Seconds you must hold Space before releasing actually fires. Releasing earlier cancels with no effect.")]
    [SerializeField] protected float windUpTime = 2f;

    [Tooltip("Seconds between finishing one attack and being able to start charging the next.")]
    [SerializeField] protected float cooldown = 0.3f;

    [Tooltip("Child object (with its own ChargeIndicator component) that draws THIS weapon's wind-up bar. Each weapon needs its own — don't share one between weapons switched independently. Optional: charging still works with no visual if left empty.")]
    [SerializeField] protected ChargeIndicator chargeIndicator;

    protected PlayerAim aim;
    protected KnockbackReceiver knockback;

    private bool charging;
    private float chargeStartTime;
    private float nextChargeAllowedTime;

    /// <summary>This weapon's charge bar footprint (width, height) in tiles — size it to fit (e.g. the Hammer's short reach vs the Bow's longer one).</summary>
    protected abstract Vector2 ChargeBarSize { get; }

    /// <summary>Called once, the instant a full-length hold is released. origin/direction are the player's position and aim at that moment.</summary>
    protected abstract void Fire(Vector2 origin, Vector2 direction);

    protected virtual void Awake()
    {
        aim = GetComponent<PlayerAim>();
        knockback = GetComponent<KnockbackReceiver>();
    }

    // Being switched away from mid-charge (or disabled on death) shouldn't leave
    // a phantom charge bar on screen, or a "still charging" state that silently
    // fires the moment this weapon is re-enabled later.
    protected virtual void OnDisable()
    {
        if (charging) CancelCharge();
    }

    protected virtual void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        bool stunned = knockback != null && knockback.IsStunned;
        if (stunned)
        {
            if (charging) CancelCharge();
            return;
        }

        if (!charging && KeyBindings.WasPressed(KeyBindings.Action.Attack) && Time.time >= nextChargeAllowedTime)
        {
            charging = true;
            chargeStartTime = Time.time;
            if (chargeIndicator != null) chargeIndicator.BeginCharge(ChargeBarSize);
        }

        if (!charging) return;

        Vector2 direction = aim != null ? aim.AimDirection : Vector2.right;
        float fraction = windUpTime > 0f ? Mathf.Clamp01((Time.time - chargeStartTime) / windUpTime) : 1f;
        if (chargeIndicator != null) chargeIndicator.Tick(transform.position, direction, fraction);

        if (KeyBindings.WasReleased(KeyBindings.Action.Attack))
        {
            if (Time.time - chargeStartTime >= windUpTime)
            {
                charging = false;
                if (chargeIndicator != null) chargeIndicator.EndCharge();
                nextChargeAllowedTime = Time.time + cooldown;
                Fire(transform.position, direction);
            }
            else
            {
                CancelCharge(); // released too early — wasted attempt, no cooldown penalty
            }
        }
    }

    private void CancelCharge()
    {
        charging = false;
        if (chargeIndicator != null) chargeIndicator.EndCharge();
    }
}
