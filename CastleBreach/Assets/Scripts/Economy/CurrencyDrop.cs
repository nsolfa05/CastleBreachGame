using UnityEngine;

/// <summary>
/// Design doc §4: a small yellow circle [Placeholder] dropped by monsters,
/// gently bobbing, despawning after 8 seconds if uncollected. The player
/// collects it by walking over it (the collider must be set as a Trigger).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CurrencyDrop : MonoBehaviour
{
    [Header("§4 Currency — all tunable")]
    [SerializeField] private int value = 3;

    [Tooltip("Seconds before an uncollected coin disappears (doc: 8).")]
    [SerializeField] private float despawnSeconds = 8f;

    [Header("Hover animation [Placeholder]")]
    [SerializeField] private float bobAmplitude = 0.12f;
    [SerializeField] private float bobSpeed = 3f;

    private Vector3 basePosition;
    private float despawnTime;

    /// <summary>Set by the monster that drops the coin (drop amounts vary per monster type).</summary>
    public void SetValue(int newValue) => value = newValue;

    private void Start()
    {
        basePosition = transform.position;
        despawnTime = Time.time + despawnSeconds;
    }

    private void Update()
    {
        transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobAmplitude);

        if (Time.time >= despawnTime)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (GameManager.Instance != null)
            GameManager.Instance.AddGold(value);
        Destroy(gameObject);
    }
}
