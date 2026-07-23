using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

/// <summary>
/// Vertical-slice version of the placement flow (design doc §10.3), without
/// the Defense Hut menu yet: press B to enter build mode. A translucent ghost
/// — a live clone of the tower prefab, so it's always the right size/shape —
/// follows the mouse, snapped to the grid (green = valid & affordable,
/// red = not). Left-click places a real tower and pays for it; build mode
/// stays active so you can place several in a row. Right-click, Esc, or B
/// again exits. The Defense Hut UI will drive this same flow later (its menu
/// click becomes the "enter build mode" trigger that B stands in for now).
///
/// The ghost is generated FROM the tower prefab at runtime and tinted — the
/// real prefab is never modified, so placed towers always keep their own
/// color. No separate hand-made ghost object is needed.
/// </summary>
public class BuildModeController : MonoBehaviour
{
    [Header("What to build")]
    [SerializeField] private GameObject towerPrefab;
    [SerializeField] private int towerCost = 150;

    [Tooltip("Footprint in tiles (Archer Tower is 2x2). Even sizes snap to a grid intersection, odd sizes to a tile center.")]
    [SerializeField] private Vector2Int footprint = new Vector2Int(2, 2);

    [Header("Validity checks")]
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap gateTilemap;

    [Tooltip("Anything on these layers blocks placement — set to Player, Enemy, Structure and King.")]
    [SerializeField] private LayerMask blockingLayers;

    [Header("Ghost preview tint [Placeholder]")]
    [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.4f);
    [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.4f);

    private bool building;
    private GameObject ghost;
    private SpriteRenderer[] ghostRenderers;

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

        // Snap the footprint's CENTER to the grid — an even footprint (2x2)
        // lands on a grid intersection so the cursor sits between the four
        // covered tiles; an odd footprint centers on a tile.
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
        Vector2 center = new Vector2(SnapAxis(mouseWorld.x, footprint.x), SnapAxis(mouseWorld.y, footprint.y));
        if (ghost != null) ghost.transform.position = center;

        Vector2Int cornerTile = TopLeftCornerTile(center);
        bool valid = IsPlacementValid(cornerTile, center);
        bool affordable = gm.Gold >= towerCost;
        TintGhost((valid && affordable) ? validColor : invalidColor);

        if (valid && affordable && mouse.leftButton.wasPressedThisFrame && gm.TrySpendGold(towerCost))
        {
            // Place the real tower — a clean clone of the prefab, full color,
            // scripts and colliders enabled. Nothing here touches its color.
            Instantiate(towerPrefab, center, Quaternion.identity);
            // Stay in build mode so several towers can be placed in a row.
        }
    }

    private void StartBuilding()
    {
        if (towerPrefab == null)
        {
            Debug.LogError("BuildModeController: assign a Tower Prefab in the Inspector.");
            return;
        }

        building = true;

        // Build the ghost from the tower prefab so it's always the right shape,
        // then strip everything that would make it act like a real tower.
        ghost = Instantiate(towerPrefab);
        ghost.name = "TowerGhost";

        foreach (var healthBar in ghost.GetComponentsInChildren<HealthBar>())
            Destroy(healthBar.gameObject);           // no health bar floating over a preview
        foreach (var behaviour in ghost.GetComponentsInChildren<MonoBehaviour>())
            behaviour.enabled = false;                // don't shoot / act
        foreach (var collider in ghost.GetComponentsInChildren<Collider2D>())
            collider.enabled = false;                 // don't block or take hits
        foreach (var body in ghost.GetComponentsInChildren<Rigidbody2D>())
            body.simulated = false;                   // no physics

        ghostRenderers = ghost.GetComponentsInChildren<SpriteRenderer>();
    }

    private void StopBuilding()
    {
        building = false;
        if (ghost != null) Destroy(ghost);
        ghost = null;
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
    /// (a half-integer) — e.g. a future 1x1 Pike Tower would center on
    /// whatever tile the cursor is over.
    /// </summary>
    private static float SnapAxis(float mouseCoord, int footprintSize) =>
        footprintSize % 2 == 0 ? Mathf.Round(mouseCoord) : Mathf.Floor(mouseCoord) + 0.5f;

    /// <summary>Doc-space tile at the footprint's top-left corner, derived from its snapped world center.</summary>
    private Vector2Int TopLeftCornerTile(Vector2 center)
    {
        // Doc "top" is toward larger world Y (row 0 = top), so the top-left
        // corner of the footprint's bounding box is (min X, max Y). Nudge
        // slightly inward so we sample inside that tile, not exactly on its edge.
        Vector2 topLeftWorld = new Vector2(center.x - footprint.x * 0.5f + 0.01f,
                                           center.y + footprint.y * 0.5f - 0.01f);
        return GridMath.WorldToTile(topLeftWorld);
    }

    private bool IsPlacementValid(Vector2Int cornerTile, Vector2 center)
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

        // Slightly smaller than the footprint so towers can sit side by side.
        Vector2 overlapSize = new Vector2(footprint.x, footprint.y) * 0.9f;
        return Physics2D.OverlapBox(center, overlapSize, 0f, blockingLayers) == null;
    }
}
