using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shows a translucent circle visualizing a tower's attack range.
/// - While placing (build mode), BuildModeController turns the circle on for
///   the ghost preview so you can judge coverage before committing.
/// - After placement, left-click the tower to toggle its circle on/off;
///   clicking anywhere else hides it.
/// Attach to the ArcherTower prefab and assign the Circle sprite
/// (Assets/Sprites/Circle) in the Inspector.
/// </summary>
[RequireComponent(typeof(ArcherTower))]
public class TowerRangeCircle : MonoBehaviour
{
    [Tooltip("The plain Circle sprite (Assets/Sprites/Circle).")]
    [SerializeField] private Sprite circleSprite;

    [Tooltip("Fill color of the range circle [Placeholder].")]
    [SerializeField] private Color circleColor = new Color(0f, 0.85f, 1f, 0.15f);

    [Tooltip("Draw order: above the map (0-2), below coins (15) and characters (20).")]
    [SerializeField] private int sortingOrder = 10;

    private SpriteRenderer circleRenderer;
    private Collider2D towerCollider;

    private void Awake()
    {
        if (circleSprite == null)
            Debug.LogWarning("TowerRangeCircle: no Circle Sprite assigned on " + name +
                             " — the range circle will be invisible until you assign one.");

        var tower = GetComponent<ArcherTower>();
        towerCollider = GetComponent<Collider2D>();

        var circleObject = new GameObject("RangeCircle");
        circleObject.transform.SetParent(transform, false);
        circleRenderer = circleObject.AddComponent<SpriteRenderer>();
        circleRenderer.sprite = circleSprite;
        circleRenderer.color = circleColor;
        circleRenderer.sortingOrder = sortingOrder;
        // The Circle sprite is 1 world unit across, so scale = diameter.
        circleObject.transform.localScale = Vector3.one * (tower.Range * 2f);
        circleRenderer.enabled = false;
    }

    /// <summary>Used by BuildModeController to show coverage on the ghost preview.</summary>
    public void ShowCircle()
    {
        if (circleRenderer != null) circleRenderer.enabled = true;
    }

    public void HideCircle()
    {
        if (circleRenderer != null) circleRenderer.enabled = false;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        var cam = Camera.main;
        if (mouse == null || cam == null || circleRenderer == null) return;
        if (!mouse.leftButton.wasPressedThisFrame) return;

        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Playing) return;

        // While build mode is active, left-clicks mean "place a tower" —
        // don't also treat them as select/deselect clicks.
        if (BuildModeController.BuildingActive) return;

        Vector2 clickPoint = cam.ScreenToWorldPoint(mouse.position.ReadValue());
        bool clickedThisTower = towerCollider != null && towerCollider.OverlapPoint(clickPoint);

        // Clicked this tower: toggle. Clicked anywhere else: hide.
        circleRenderer.enabled = clickedThisTower && !circleRenderer.enabled;
    }
}
