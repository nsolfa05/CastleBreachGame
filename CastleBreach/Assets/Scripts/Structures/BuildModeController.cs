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

        // Snap the footprint so its top-left tile is the tile under the cursor.
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
        Vector2Int cornerTile = GridMath.WorldToTile(mouseWorld);
        Vector2 center = FootprintCenterWorld(cornerTile);
        ghost.transform.position = center;

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

    private Vector2 FootprintCenterWorld(Vector2Int cornerTile)
    {
        Vector2 cornerCenter = GridMath.TileCenterWorld(cornerTile);
        // Doc rows grow downward, so extra rows extend toward smaller world y.
        return new Vector2(cornerCenter.x + (footprint.x - 1) * 0.5f,
                           cornerCenter.y - (footprint.y - 1) * 0.5f);
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
