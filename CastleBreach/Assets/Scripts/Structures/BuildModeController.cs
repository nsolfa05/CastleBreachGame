using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

/// <summary>
/// Vertical-slice version of the placement flow (design doc §10.3), without
/// the Defense Hut menu yet.
///
/// Controls: press a NUMBER KEY (1, 2, 3, ...) to pick up that entry from
/// the Build Options list — a translucent ghost (a live clone of the
/// structure's prefab, so it's always the right size/shape) follows the
/// mouse, snapped to the grid (green = valid & affordable, red = not).
/// Left-click places a real structure and pays for it; you keep carrying the
/// ghost to place more. Press B to toggle carrying the last-used option.
/// Right-click or Esc puts it away. The Defense Hut UI (a later phase)
/// replaces these hotkeys — its menu click becomes the pick-up trigger.
///
/// The ghost is generated FROM the prefab at runtime and tinted — the real
/// prefab is never modified, so placed structures always keep their colors.
/// </summary>
public class BuildModeController : MonoBehaviour
{
    /// <summary>True while the player is carrying a placement ghost — other
    /// click handlers (e.g. tower range-circle toggling) ignore clicks then.</summary>
    public static bool BuildingActive { get; private set; }

    [System.Serializable]
    public class BuildOption
    {
        [Tooltip("Shown in logs/UI later; pick something readable, e.g. \"Archer Tower\".")]
        public string displayName = "Structure";

        public GameObject prefab;
        public int cost = 150;

        [Tooltip("Footprint in tiles. Even sizes (2x2) snap to grid intersections, odd sizes (1x1) to tile centers.")]
        public Vector2Int footprint = new Vector2Int(2, 2);
    }

    [Header("What can be built (press number key 1, 2, 3... to pick up)")]
    [SerializeField] private List<BuildOption> buildOptions = new List<BuildOption>();

    [Header("Validity checks")]
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap gateTilemap;

    [Tooltip("Anything on these layers blocks placement — set to Player, Enemy, Structure and King.")]
    [SerializeField] private LayerMask blockingLayers;

    [Header("Ghost preview tint [Placeholder]")]
    [Tooltip("Opaque enough to see over any ground color; the green/red still reads as a tint over the structure shape.")]
    [SerializeField] private Color validColor = new Color(0.35f, 1f, 0.35f, 0.7f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.7f);

    [Tooltip("Sorting order for the ghost — high so it always draws above the map, towers and characters.")]
    [SerializeField] private int ghostSortingOrder = 100;

    private bool building;
    private int selectedIndex;

    private void Awake()
    {
        // Statics survive a scene reload (pressing R after win/lose) — make
        // sure a restart never starts with a stale "still building" flag.
        BuildingActive = false;
    }

    // Named 'ghostInstance' (not 'ghost') on purpose: an earlier version had a
    // serialized SpriteRenderer field literally named 'ghost'. Reusing that
    // name here made Unity try to reconcile this runtime field with the old
    // saved reference and warn about a type mismatch. A distinct name lets
    // Unity drop the stale saved data cleanly.
    private GameObject ghostInstance;
    private SpriteRenderer[] ghostRenderers;

    private BuildOption CurrentOption =>
        (buildOptions.Count > 0 && selectedIndex >= 0 && selectedIndex < buildOptions.Count)
            ? buildOptions[selectedIndex]
            : null;

    private void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Playing)
        {
            StopBuilding();
            return;
        }

        // Number keys pick up a specific structure (and switch mid-carry).
        for (int i = 0; i < buildOptions.Count && i < 9; i++)
        {
            if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
            {
                selectedIndex = i;
                StopBuilding();
                StartBuilding();
                break;
            }
        }

        // B toggles carrying the last-used option.
        if (keyboard.bKey.wasPressedThisFrame)
        {
            if (building) StopBuilding();
            else StartBuilding();
        }
        if (!building) return;

        if (keyboard.escapeKey.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
        {
            StopBuilding();
            return;
        }

        var option = CurrentOption;
        if (option == null) { StopBuilding(); return; }

        // Snap the footprint's CENTER to the grid — an even footprint (2x2)
        // lands on a grid intersection so the cursor sits between the four
        // covered tiles; an odd footprint (1x1) centers on a tile.
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
        Vector2 center = new Vector2(SnapAxis(mouseWorld.x, option.footprint.x),
                                     SnapAxis(mouseWorld.y, option.footprint.y));
        if (ghostInstance != null) ghostInstance.transform.position = center;

        Vector2Int cornerTile = TopLeftCornerTile(center, option.footprint);
        bool valid = IsPlacementValid(cornerTile, center, option.footprint);
        bool affordable = gm.Gold >= option.cost;
        TintGhost((valid && affordable) ? validColor : invalidColor);

        if (valid && affordable && mouse.leftButton.wasPressedThisFrame && gm.TrySpendGold(option.cost))
        {
            // Place the real structure — a clean clone of the prefab, full
            // color, scripts and colliders enabled. Force it active in case
            // the prefab asset was ever saved inactive (a prefab saved
            // inactive would otherwise spawn silent, invisible clones).
            var placed = Instantiate(option.prefab, center, Quaternion.identity);
            placed.SetActive(true);
            // Keep carrying the ghost so several can be placed in a row.
        }
    }

    private void StartBuilding()
    {
        var option = CurrentOption;
        if (option == null || option.prefab == null)
        {
            Debug.LogError("BuildModeController: the selected Build Option has no prefab assigned.");
            return;
        }

        building = true;
        BuildingActive = true;

        // Build the ghost from the structure prefab so it's always the right
        // shape, then strip everything that would make it act like the real thing.
        ghostInstance = Instantiate(option.prefab);
        ghostInstance.name = option.displayName + " Ghost";
        ghostInstance.SetActive(true); // same defensive reason as placement above

        foreach (var healthBar in ghostInstance.GetComponentsInChildren<HealthBar>())
            Destroy(healthBar.gameObject);           // no health bar floating over a preview
        foreach (var behaviour in ghostInstance.GetComponentsInChildren<MonoBehaviour>())
            behaviour.enabled = false;                // don't shoot / generate gold / act
        foreach (var collider in ghostInstance.GetComponentsInChildren<Collider2D>())
            collider.enabled = false;                 // don't block or take hits
        foreach (var body in ghostInstance.GetComponentsInChildren<Rigidbody2D>())
            body.simulated = false;                   // no physics

        // Show the attack-range circle (if this structure has one) so
        // coverage is visible while choosing a spot. (Created in Awake,
        // before the strip loop disabled behaviours — the call still works.)
        var rangeCircle = ghostInstance.GetComponent<TowerRangeCircle>();
        if (rangeCircle != null) rangeCircle.ShowCircle();

        // Tint every renderer EXCEPT the range/dead-zone circles (they keep
        // their own look instead of going opaque green/red with the body).
        var allRenderers = ghostInstance.GetComponentsInChildren<SpriteRenderer>();
        var tintable = new List<SpriteRenderer>();
        foreach (var renderer in allRenderers)
        {
            if (renderer.gameObject.name == "RangeCircle" ||
                renderer.gameObject.name == "DeadZoneCircle") continue;
            renderer.sortingOrder = ghostSortingOrder; // always draw on top so it's never hidden
            tintable.Add(renderer);
        }
        ghostRenderers = tintable.ToArray();
    }

    private void StopBuilding()
    {
        building = false;
        BuildingActive = false;
        if (ghostInstance != null) Destroy(ghostInstance);
        ghostInstance = null;
        ghostRenderers = null;
    }

    private void TintGhost(Color color)
    {
        if (ghostRenderers == null) return;
        foreach (var renderer in ghostRenderers)
            if (renderer != null) renderer.color = color;
    }

    /// <summary>
    /// Snaps one world axis to the nearest valid footprint-center position:
    /// an even footprint size centers on a grid intersection (a whole
    /// number), an odd footprint size centers on a tile's own center
    /// (a half-integer) — so the 1x1 Pike Tower centers on whatever tile
    /// the cursor is over.
    /// </summary>
    private static float SnapAxis(float mouseCoord, int footprintSize) =>
        footprintSize % 2 == 0 ? Mathf.Round(mouseCoord) : Mathf.Floor(mouseCoord) + 0.5f;

    /// <summary>Doc-space tile at the footprint's top-left corner, derived from its snapped world center.</summary>
    private Vector2Int TopLeftCornerTile(Vector2 center, Vector2Int footprint)
    {
        // Doc "top" is toward larger world Y (row 0 = top), so the top-left
        // corner of the footprint's bounding box is (min X, max Y). Nudge
        // slightly inward so we sample inside that tile, not exactly on its edge.
        Vector2 topLeftWorld = new Vector2(center.x - footprint.x * 0.5f + 0.01f,
                                           center.y + footprint.y * 0.5f - 0.01f);
        return GridMath.WorldToTile(topLeftWorld);
    }

    private bool IsPlacementValid(Vector2Int cornerTile, Vector2 center, Vector2Int footprint)
    {
        for (int dx = 0; dx < footprint.x; dx++)
        {
            for (int dy = 0; dy < footprint.y; dy++)
            {
                var tile = new Vector2Int(cornerTile.x + dx, cornerTile.y + dy);
                if (!GridMath.InBounds(tile)) return false;

                var cell = GridMath.TileToCell(tile);
                if (wallTilemap != null && wallTilemap.HasTile(cell)) return false;
                if (gateTilemap != null && gateTilemap.HasTile(cell)) return false;
            }
        }

        // Slightly smaller than the footprint so structures can sit side by side.
        Vector2 overlapSize = new Vector2(footprint.x, footprint.y) * 0.9f;
        return Physics2D.OverlapBox(center, overlapSize, 0f, blockingLayers) == null;
    }
}
