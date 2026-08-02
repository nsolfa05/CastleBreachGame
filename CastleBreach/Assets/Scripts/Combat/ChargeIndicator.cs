using UnityEngine;

/// <summary>
/// Pure visuals for a "hold to charge" weapon (Bow, Hammer, Fire Staff — any
/// future wind-up attack): a semi-transparent outline rectangle appears the
/// moment charging starts, with a second rectangle inside it that fills from
/// empty to full as the charge progresses — the classic charge-bar look. Owns
/// no timing/input; the weapon calls BeginCharge/Tick/EndCharge and this just
/// draws. Position and facing are driven every frame via Tick so it tracks the
/// player and follows the mouse aim live while charging.
///
/// Reuses the same "two placeholder boxes" approach as the enemy
/// TelegraphedAreaAttack, but anchored to a moving origin (the player) and
/// facing (aim direction) instead of a locked world position, and the fill
/// grows along one axis (a bar) rather than scaling the whole box from its
/// center (an area).
/// </summary>
public class ChargeIndicator : MonoBehaviour
{
    [Tooltip("The plain Square sprite (Assets/Sprites/Square) — used for both boxes.")]
    [SerializeField] private Sprite boxSprite;

    [Tooltip("Outline box color — shown for the whole charge, doesn't fill.")]
    [SerializeField] private Color outlineColor = new Color(0.9f, 0.9f, 0.9f, 0.25f);

    [Tooltip("Fill bar color — grows from empty to full as the charge completes.")]
    [SerializeField] private Color fillColor = new Color(1f, 0.85f, 0.2f, 0.55f);

    [Tooltip("Draw order for both boxes. High so the charge bar is never hidden behind terrain/structures.")]
    [SerializeField] private int sortingOrder = 25;

    private SpriteRenderer outlineBox;
    private SpriteRenderer fillBox;
    private Vector2 sizeWorld;
    private bool charging;

    private void EnsureBoxes()
    {
        if (outlineBox == null) outlineBox = CreateBox("ChargeOutline", sortingOrder);
        if (fillBox == null) fillBox = CreateBox("ChargeFill", sortingOrder + 1);
    }

    private SpriteRenderer CreateBox(string boxName, int order)
    {
        var go = new GameObject(boxName);
        go.transform.SetParent(transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = boxSprite;
        renderer.sortingOrder = order;
        renderer.enabled = false;
        go.AddComponent<CombatFxVisual>(); // see CombatFxVisual — exempts this from PlayerRespawn's blanket re-enable
        return renderer;
    }

    /// <summary>Start charging: show the outline immediately, fill starts empty.
    /// sizeTiles is the bar's full (width, height) footprint in tiles.</summary>
    public void BeginCharge(Vector2 sizeTiles)
    {
        if (boxSprite == null)
        {
            Debug.LogWarning("ChargeIndicator: no Box Sprite assigned — charge bar will be invisible.");
            return;
        }
        EnsureBoxes();

        sizeWorld = sizeTiles;
        charging = true;

        outlineBox.transform.localRotation = Quaternion.identity;
        outlineBox.transform.localScale = new Vector3(sizeWorld.x, sizeWorld.y, 1f);
        outlineBox.color = outlineColor;
        outlineBox.enabled = true;

        fillBox.color = fillColor;
        fillBox.enabled = true;
        UpdateFill(0f);
    }

    /// <summary>Call every frame while charging: repositions/faces the bar at
    /// origin+direction*half-reach and updates the fill from chargeFraction (0..1).</summary>
    public void Tick(Vector2 origin, Vector2 direction, float chargeFraction)
    {
        if (!charging) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Vector2 center = origin + direction * (sizeWorld.x * 0.5f);

        transform.position = center;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        UpdateFill(chargeFraction);
    }

    private void UpdateFill(float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        // Anchored to the near edge (toward the player) and growing outward —
        // a real charge bar, not a box growing from its own center.
        float filledWidth = sizeWorld.x * fraction;
        fillBox.transform.localPosition = new Vector3(-(sizeWorld.x - filledWidth) * 0.5f, 0f, 0f);
        fillBox.transform.localRotation = Quaternion.identity;
        fillBox.transform.localScale = new Vector3(filledWidth, sizeWorld.y, 1f);
    }

    /// <summary>Stop charging (fired, or canceled) — hide both boxes.</summary>
    public void EndCharge()
    {
        charging = false;
        if (outlineBox != null) outlineBox.enabled = false;
        if (fillBox != null) fillBox.enabled = false;
    }

    private void OnDestroy()
    {
        // The boxes are children, so Unity would clean them up anyway — explicit
        // for clarity and to match TelegraphedAreaAttack's convention.
        if (outlineBox != null) Destroy(outlineBox.gameObject);
        if (fillBox != null) Destroy(fillBox.gameObject);
    }
}
