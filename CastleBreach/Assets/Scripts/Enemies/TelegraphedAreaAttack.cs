using UnityEngine;

/// <summary>
/// Pure visuals for a monster's telegraphed box attack (Cyclops §7.3),
/// driven entirely by MonsterAI — this component owns no timing or damage
/// logic, it just draws three placeholder boxes on command:
///  - a base box: the full attack AREA, shown for the whole wind-up.
///  - a fill box: grows from nothing to the full area over the wind-up,
///    the "charging" indicator.
///  - a slam box: flashed at the moment of impact.
///
/// The boxes are locked to a WORLD position (not parented to the monster),
/// matching the design's "keeps attacking that zone even if the target
/// moves out" — the monster can drift/get nudged without the box following.
/// Add this to the shared Monster prefab and assign the Square sprite; only
/// definitions with Uses Telegraphed Area Attack ever trigger it.
/// </summary>
public class TelegraphedAreaAttack : MonoBehaviour
{
    [Tooltip("The plain Square sprite (Assets/Sprites/Square) — used for all three boxes.")]
    [SerializeField] private Sprite boxSprite;

    [Tooltip("Draw order for the telegraph boxes. Low (5) = a ground marker beneath structures/characters.")]
    [SerializeField] private int sortingOrder = 5;

    private SpriteRenderer baseBox;
    private SpriteRenderer fillBox;
    private SpriteRenderer slamBox;

    private Vector2 center;
    private Vector2 sizeWorld;
    private float telegraphStart;
    private float telegraphDuration = 1f;
    private bool telegraphing;
    private float slamHideTime;

    private void EnsureBoxes()
    {
        if (baseBox == null) baseBox = CreateBox("TelegraphBase", sortingOrder);
        if (fillBox == null) fillBox = CreateBox("TelegraphFill", sortingOrder + 1);
        if (slamBox == null) slamBox = CreateBox("TelegraphSlam", sortingOrder + 2);
    }

    private SpriteRenderer CreateBox(string boxName, int order)
    {
        var go = new GameObject(boxName);
        go.transform.SetParent(null, false); // world space — locked, not following the monster
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = boxSprite;
        renderer.sortingOrder = order;
        renderer.enabled = false;
        return renderer;
    }

    /// <summary>Start the wind-up: draw the base box and begin filling the darker box.</summary>
    public void BeginTelegraph(Vector2 boxCenter, Vector2 sizeTiles, float duration, Color baseColor, Color fillColor)
    {
        if (boxSprite == null)
        {
            Debug.LogWarning("TelegraphedAreaAttack: no Box Sprite assigned — telegraph will be invisible.");
            return;
        }
        EnsureBoxes();

        center = boxCenter;
        sizeWorld = sizeTiles; // 1 tile = 1 world unit; Square sprite is 1 unit, so scale == size
        telegraphStart = Time.time;
        telegraphDuration = Mathf.Max(0.0001f, duration);
        telegraphing = true;

        baseBox.transform.position = center;
        baseBox.transform.localScale = new Vector3(sizeWorld.x, sizeWorld.y, 1f);
        baseBox.color = baseColor;
        baseBox.enabled = true;

        fillBox.transform.position = center;
        fillBox.transform.localScale = Vector3.zero;
        fillBox.color = fillColor;
        fillBox.enabled = true;
    }

    /// <summary>Impact: hide the wind-up boxes and flash the slam box.</summary>
    public void TriggerSlam(Color slamBoxColor, float flashSeconds)
    {
        EnsureBoxes();
        telegraphing = false;
        if (baseBox != null) baseBox.enabled = false;
        if (fillBox != null) fillBox.enabled = false;

        slamBox.transform.position = center;
        slamBox.transform.localScale = new Vector3(sizeWorld.x, sizeWorld.y, 1f);
        slamBox.color = slamBoxColor;
        slamBox.enabled = true;
        slamHideTime = Time.time + Mathf.Max(0.01f, flashSeconds);
    }

    /// <summary>Abort the wind-up without slamming (target gone, game over, etc.).</summary>
    public void Cancel()
    {
        telegraphing = false;
        if (baseBox != null) baseBox.enabled = false;
        if (fillBox != null) fillBox.enabled = false;
    }

    private void Update()
    {
        if (telegraphing && fillBox != null)
        {
            float t = Mathf.Clamp01((Time.time - telegraphStart) / telegraphDuration);
            fillBox.transform.localScale = new Vector3(sizeWorld.x * t, sizeWorld.y * t, 1f);
        }

        if (slamBox != null && slamBox.enabled && Time.time >= slamHideTime)
            slamBox.enabled = false;
    }

    private void OnDestroy()
    {
        // The boxes aren't children, so destroy them explicitly with the monster.
        if (baseBox != null) Destroy(baseBox.gameObject);
        if (fillBox != null) Destroy(fillBox.gameObject);
        if (slamBox != null) Destroy(slamBox.gameObject);
    }
}
