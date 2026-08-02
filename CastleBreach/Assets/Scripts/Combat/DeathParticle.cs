using UnityEngine;

/// <summary>
/// A single small square debris particle: launched with an outward velocity,
/// falls under a bit of gravity, fades out, and destroys itself after its
/// lifetime — spawned by DeathEffect's burst, no prefab needed (same
/// "new GameObject + AddComponent + static Spawn()" convention as ImpactMark/
/// BurnZone).
/// </summary>
public class DeathParticle : MonoBehaviour
{
    private SpriteRenderer sr;
    private Vector2 velocity;
    private float gravity;
    private float lifetime;
    private float age;
    private Color baseColor;

    public static DeathParticle Spawn(Vector2 position, Vector2 velocity, float gravity, float size,
        Color color, Sprite sprite, float lifetime, int sortingOrder)
    {
        var go = new GameObject("DeathParticle");
        go.transform.position = position;
        go.transform.localScale = new Vector3(size, size, 1f);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        var particle = go.AddComponent<DeathParticle>();
        particle.sr = renderer;
        particle.velocity = velocity;
        particle.gravity = gravity;
        particle.lifetime = Mathf.Max(0.01f, lifetime);
        particle.baseColor = color;
        return particle;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        velocity += Vector2.down * gravity * Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);

        // Fades out over its lifetime rather than just vanishing at the end.
        float fraction = 1f - age / lifetime;
        sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * fraction);
    }
}
