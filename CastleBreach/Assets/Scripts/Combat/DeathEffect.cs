using UnityEngine;

/// <summary>
/// Shared "you just died" visual, meant for both the Player and every
/// monster: the body tints red and lingers in place for Body Lifetime
/// Seconds before actually disappearing, while a burst of small square
/// DeathParticles scatters outward from it at Knockback Amount speed.
///
/// Purely visual — respawn logic (PlayerRespawn) and permanent-death logic
/// (MonsterAI: releasing its slot, dropping currency, the Killed event) stay
/// exactly where they already are. Both simply call Play() and wait however
/// long it reports back before doing their own hide/destroy step, so this
/// component is the one place that controls how long the corpse lingers —
/// change Body Lifetime Seconds here, nowhere else.
/// </summary>
public class DeathEffect : MonoBehaviour
{
    [Header("Body")]
    [Tooltip("Tint applied to every SpriteRenderer on this object the instant it dies.")]
    [SerializeField] private Color deathTint = new Color(0.75f, 0.15f, 0.15f);

    [Tooltip("How long (seconds) the tinted body stays visible before the caller (PlayerRespawn / MonsterAI) hides or destroys it. Purely how long the RED VISUAL lingers — doesn't by itself despawn or disable anything.")]
    [SerializeField] private float bodyLifetimeSeconds = 1f;

    [Header("Particle Burst")]
    [Tooltip("How many small square particles burst outward on death.")]
    [SerializeField] private int particleCount = 10;

    [Tooltip("How fast the particles launch outward — this effect's own \"knockback\" amount, separate from any combat knockback. Higher = particles fly further before Particle Gravity pulls them down.")]
    [SerializeField] private float knockbackAmount = 3f;

    [Tooltip("Downward acceleration applied to each particle after launch (world units/sec²). 0 = particles fly outward in a straight line.")]
    [SerializeField] private float particleGravity = 4f;

    [Tooltip("Particle size (world units — roughly the square's edge length).")]
    [SerializeField] private float particleSize = 0.12f;

    [Tooltip("How long (seconds) each particle lives before fading out and disappearing.")]
    [SerializeField] private float particleLifetimeSeconds = 0.6f;

    [Tooltip("Particle color — defaults to the same red as Death Tint, but editable separately (e.g. bone-white debris for a Skeleton).")]
    [SerializeField] private Color particleColor = new Color(0.75f, 0.15f, 0.15f);

    [Tooltip("The plain Square sprite (Assets/Sprites/Square) — used for every particle.")]
    [SerializeField] private Sprite particleSprite;

    [Tooltip("Draw order for particles — high so the burst is never hidden behind terrain/structures.")]
    [SerializeField] private int particleSortingOrder = 20;

    private SpriteRenderer[] bodyRenderers;
    private Color[] originalColors;

    private void Awake()
    {
        bodyRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[bodyRenderers.Length];
        for (int i = 0; i < bodyRenderers.Length; i++)
            originalColors[i] = bodyRenderers[i].color;
    }

    /// <summary>
    /// Tints the body, fires the particle burst, and schedules the tint to
    /// restore itself after Body Lifetime Seconds (so a Skeleton's bone-pile
    /// revive, or the player's respawn, never comes back permanently red).
    /// Returns Body Lifetime Seconds so the caller knows how long to wait
    /// before its own hide/destroy step.
    /// </summary>
    public float Play()
    {
        foreach (var renderer in bodyRenderers)
            if (renderer != null) renderer.color = deathTint;

        CancelInvoke(nameof(RestoreTint));
        Invoke(nameof(RestoreTint), bodyLifetimeSeconds);

        SpawnParticleBurst();

        return bodyLifetimeSeconds;
    }

    private void RestoreTint()
    {
        for (int i = 0; i < bodyRenderers.Length; i++)
            if (bodyRenderers[i] != null) bodyRenderers[i].color = originalColors[i];
    }

    private void SpawnParticleBurst()
    {
        Vector2 origin = transform.position;
        for (int i = 0; i < particleCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 velocity = direction * knockbackAmount * Random.Range(0.6f, 1f);

            DeathParticle.Spawn(origin, velocity, particleGravity, particleSize,
                particleColor, particleSprite, particleLifetimeSeconds, particleSortingOrder);
        }
    }
}
