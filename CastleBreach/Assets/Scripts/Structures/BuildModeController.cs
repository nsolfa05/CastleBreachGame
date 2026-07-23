using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

/// <summary>
/// Vertical-slice version of the placement flow (design doc §10.3), without
/// the Defense Hut menu yet: press B to start placing an Archer Tower, a
/// ghost preview follows the mouse snapped to the grid (green = valid and
/// affordable, red = not), left-click places and pays, right-click or Esc
/// cancels. The Defense Hut UI will drive this same flow later.
/// </summary>
public class BuildModeController : MonoBehaviour
{
    [Header("What to build")]
    [SerializeField] private GameObject towerPrefab;
    [SerializeField] private int towerCost = 150;

    [Tooltip("Footprint in tiles (Archer Tower is 2x2).")]
    [SerializeField] private Vector2Int footprint = new Vector2Int(2, 2);

    [Header("Validity checks")]
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap gateTilemap;

    [Tooltip("Anything on these layers blocks placement — set to Player, Enemy, Structure and King.")]
    [SerializeField] private LayerMask blockingLayers;

    [Header("Ghost preview [Placeholder]")]
    [Tooltip("A Square sprite object in the scene; this script moves, colors, shows and hides it.")]
    [SerializeField] private SpriteRenderer ghost;
    [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.4f);
    [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.4f);

    private bool building;

    private void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null || ghost == null) return;

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

        // Snap the footprint's CENTER to the grid — for an even-sized footprint
        // (2x2) that lands on a grid intersection, so the cursor sits between
        // the four covered tiles rather than pinned to one tile's corner.
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
        Vector2 center = new Vector2(SnapAxis(mouseWorld.x, footprint.x), SnapAxis(mouseWorld.y, footprint.y));
        ghost.transform.position = center;

        Vector2Int cornerTile = TopLeftCornerTile(center);
        bool valid = IsPlacementValid(cornerTile, center);
        bool affordable = gm.Gold >= towerCost;
        ghost.color = (valid && affordable) ? validColor : invalidColor;

        if (valid && affordable && mouse.leftButton.wasPressedThisFrame && gm.TrySpendGold(towerCost))
        {
            Instantiate(towerPrefab, center, Quaternion.identity);
            StopBuilding();
        }
    }

    private void StartBuilding()
    {
        building = true;
        ghost.transform.localScale = new Vector3(footprint.x, footprint.y, 1f);
        ghost.gameObject.SetActive(true);
    }

    private void StopBuilding()
    {
        if (!building && !ghost.gameObject.activeSelf) return;
        building = false;
        ghost.gameObject.SetActive(false);
    }

    /// <summary>
    /// Snaps one world axis to the nearest valid footprint-center position:
    /// an even footprint size centers on a grid intersection (a whole
    /// number), an odd footprint size centers on a tile's own center
    /// (a half-integer) — e.g. a future 1x1 Pike Tower would center on
    /// whatever tile the cursor is over, same as before this change.
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
