using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shows a translucent circle visualizing an attack tower's range.
/// - While placing (build mode), BuildModeController turns the circle on for
///   the ghost preview so you can judge coverage before committing.
/// - After placement, left-click the tower to toggle its circle on/off;
///   clicking anywhere else hides it.
/// If the tower has a dead zone (Catapult's Min Range), a second red-tinted
/// inner circle shows the area it CANNOT hit.
/// Attach to any AttackTower prefab and assign the Circle sprite
/// (Assets/Sprites/Circle) in the Inspector.
/// </summary>
[RequireComponent(typeof(AttackTower))]
public class TowerRangeCircle : MonoBehaviour
{
    [Tooltip("The plain Circle sprite (Assets/Sprites/Circle).")]
    [SerializeField] private Sprite circleSprite;

    [Tooltip("Fill color of the range circle [Placeholder].")]
    [SerializeField] private Color circleColor = new Color(0f, 0.85f, 1f, 0.15f);

    [Tooltip("Fill color of the dead-zone circle (Catapult) [Placeholder].")]
    [SerializeField] private Color deadZoneColor = new Color(1f, 0.25f, 0.25f, 0.18f);

    [Tooltip("Draw order: above the map (0-2), below coins (15) and characters (20).")]
    [SerializeField] private int sortingOrder = 10;

    private SpriteRenderer rangeRenderer;
    private SpriteRenderer deadZoneRenderer;
    private Collider2D towerCollider;

    private void Awake()
    {
        if (circleSprite == null)
            Debug.LogWarning("TowerRangeCircle: no Circle Sprite assigned on " + name +
                             " — the range circle will be invisible until you assign one.");

        var tower = GetComponent<AttackTower>();
        towerCollider = GetComponent<Collider2D>();

        rangeRenderer = CreateCircle("RangeCircle", tower.Range, circleColor, sortingOrder);
        if (tower.MinRange > 0f)
            deadZoneRenderer = CreateCircle("DeadZoneCircle", tower.MinRange, deadZoneColor, sortingOrder + 1);

        SetVisible(false);
    }

    private SpriteRenderer CreateCircle(string childName, float radius, Color color, int order)
    {
        var circleObject = new GameObject(childName);
        circleObject.transform.SetParent(transform, false);

        var renderer = circleObject.AddComponent<SpriteRenderer>();
        renderer.sprite = circleSprite;
        renderer.color = color;
        renderer.sortingOrder = order;

        // The Circle sprite is 1 world unit across, so scale = diameter — but
        // as a child it inherits the tower's own scale (e.g. a 2x-scaled tower
        // body would double the circle), so divide that back out.
        Vector3 parentScale = transform.lossyScale;
        float diameter = radius * 2f;
        circleObject.transform.localScale = new Vector3(
            diameter / Mathf.Max(0.0001f, parentScale.x),
            diameter / Mathf.Max(0.0001f, parentScale.y),
            1f);

        return renderer;
    }

    private void SetVisible(bool visible)
    {
        if (rangeRenderer != null) rangeRenderer.enabled = visible;
        if (deadZoneRenderer != null) deadZoneRenderer.enabled = visible;
    }

    private bool IsVisible => rangeRenderer != null && rangeRenderer.enabled;

    /// <summary>Used by BuildModeController to show coverage on the ghost preview.</summary>
    public void ShowCircle() => SetVisible(true);

    public void HideCircle() => SetVisible(false);

    private void Update()
    {
        var mouse = Mouse.current;
        var cam = Camera.main;
        if (mouse == null || cam == null || rangeRenderer == null) return;
        if (!mouse.leftButton.wasPressedThisFrame) return;

        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Playing) return;

        // While build mode is active, left-clicks mean "place a tower" —
        // don't also treat them as select/deselect clicks.
        if (BuildModeController.BuildingActive) return;

        Vector2 clickPoint = cam.ScreenToWorldPoint(mouse.position.ReadValue());
        bool clickedThisTower = towerCollider != null && towerCollider.OverlapPoint(clickPoint);

        // Clicked this tower: toggle. Clicked anywhere else: hide.
        SetVisible(clickedThisTower && !IsVisible);
    }
}
