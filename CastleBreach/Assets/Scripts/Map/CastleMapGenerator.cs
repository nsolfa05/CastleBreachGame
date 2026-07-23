using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Generates the starting castle map from design doc §3.2–3.3:
/// grass ground everywhere, 2-tile-thick walls on all four sides,
/// and four gates carved into the inner wall column/row.
///
/// The layout is data (the region lists below), not code — future campaign
/// maps just use different regions, per §3.4.
///
/// HOW TO USE IN THE EDITOR: fill in the Inspector fields, then right-click
/// the component header and choose "Generate Map". Safe to re-run anytime.
/// </summary>
public class CastleMapGenerator : MonoBehaviour
{
    [Header("Tilemaps (children of the Grid object)")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap gateTilemap;

    [Header("Tiles [Placeholder] — swap for real art later, no code changes needed.")]
    [Tooltip("Color lives on each Tile asset itself (its own Color field), not here — " +
             "edit GroundTile/WallTile/GateTile directly to change color.")]
    [SerializeField] private TileBase groundTile;
    [SerializeField] private TileBase wallTile;
    [SerializeField] private TileBase gateTile;

    [Header("Layout (design doc §3.2 — walls)")]
    [SerializeField] private List<TileRegion> wallRegions = new List<TileRegion>
    {
        new TileRegion { name = "West wall",  from = "A1",  to = "B30"  },
        new TileRegion { name = "North wall", from = "B1",  to = "AL2"  },
        new TileRegion { name = "East wall",  from = "AM1", to = "AN30" },
        new TileRegion { name = "South wall", from = "C29", to = "AL30" },
    };

    [Header("Layout (design doc §3.3 — gates, carved out of the walls)")]
    [SerializeField] private List<TileRegion> gateRegions = new List<TileRegion>
    {
        new TileRegion { name = "West",  from = "B14",  to = "B17"  },
        new TileRegion { name = "East",  from = "AM14", to = "AM17" },
        new TileRegion { name = "South", from = "R29",  to = "W29"  },
        new TileRegion { name = "North", from = "R2",   to = "W2"   },
    };

    /// <summary>Used by the WaveSpawner to find where monsters may spawn.</summary>
    public IReadOnlyList<TileRegion> GateRegions => gateRegions;

    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        if (groundTilemap == null || wallTilemap == null || gateTilemap == null)
        {
            Debug.LogError("CastleMapGenerator: assign all three Tilemaps in the Inspector first.");
            return;
        }

        groundTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
        gateTilemap.ClearAllTiles();

        for (int col = 0; col < GridMath.Columns; col++)
            for (int row = 0; row < GridMath.Rows; row++)
                groundTilemap.SetTile(GridMath.TileToCell(new Vector2Int(col, row)), groundTile);

        foreach (var region in wallRegions)
            foreach (var tile in region.Tiles())
                wallTilemap.SetTile(GridMath.TileToCell(tile), wallTile);

        // Gates: remove the wall tile (so the gate is passable) and draw the gate tile.
        foreach (var region in gateRegions)
            foreach (var tile in region.Tiles())
            {
                wallTilemap.SetTile(GridMath.TileToCell(tile), null);
                gateTilemap.SetTile(GridMath.TileToCell(tile), gateTile);
            }

        Debug.Log($"Castle map generated: {GridMath.Columns}x{GridMath.Rows} tiles, " +
                  $"{wallRegions.Count} wall regions, {gateRegions.Count} gates.");

#if UNITY_EDITOR
        // Painting tile colors via script doesn't reliably mark the scene as
        // having unsaved changes on its own — without this, the colors can
        // look correct in the Editor but silently fail to persist through a
        // save or a Play Mode session, reverting to the tiles' default color.
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(groundTilemap);
            EditorUtility.SetDirty(wallTilemap);
            EditorUtility.SetDirty(gateTilemap);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    /// <summary>True if a wall occupies this tile (gates count as open).</summary>
    public bool IsWall(Vector2Int tile) =>
        wallTilemap != null && wallTilemap.HasTile(GridMath.TileToCell(tile));

    public bool IsGate(Vector2Int tile) =>
        gateTilemap != null && gateTilemap.HasTile(GridMath.TileToCell(tile));

    private void Start()
    {
        // Always regenerate at runtime, unconditionally: the map is fully
        // described by the serialized data above (regions + colors), so
        // there's no need to depend on whatever got painted and saved in
        // the Editor beforehand — every Play session repaints itself fresh.
        GenerateMap();
    }
}
