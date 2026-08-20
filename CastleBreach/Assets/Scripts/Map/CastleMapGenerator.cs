using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Generates the castle map from design doc §3.2–3.3: 2-tile-thick walls on
/// all four sides and four gates carved into the inner wall column/row.
/// Walls/gates are data (the region lists below), not code — future campaign
/// maps just use different regions, per §3.4.
///
/// Ground is a different story on purpose: it's hand-painted per level with
/// the Tile Palette (each level's own grass/dirt/flower arrangement), so
/// nothing here ever auto-fills or clears it — see "Fill Ground (Reset)"
/// below for the one explicit, manual exception.
///
/// HOW TO USE IN THE EDITOR:
/// - Right-click the component header → "Generate Walls && Gates" any time
///   you add/edit a region and want to see it (also runs automatically on
///   Play). Never touches ground.
/// - Right-click → "Fill Ground (Reset)" ONLY when you want to wipe the
///   ground layer back to solid ground tile and start painting over — this
///   destroys any hand-painted ground tiles, so don't reach for it out of
///   habit the way the old combined "Generate Map" used to be re-run.
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

    [ContextMenu("Generate Walls && Gates")]
    public void GenerateWallsAndGates()
    {
        if (wallTilemap == null || gateTilemap == null)
        {
            Debug.LogError("CastleMapGenerator: assign the Wall and Gate Tilemaps in the Inspector first.");
            return;
        }

        wallTilemap.ClearAllTiles();
        gateTilemap.ClearAllTiles();

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

        Debug.Log($"Castle walls/gates generated: {wallRegions.Count} wall regions, " +
                  $"{gateRegions.Count} gates. Ground untouched.");

#if UNITY_EDITOR
        // Painting tile colors via script doesn't reliably mark the scene as
        // having unsaved changes on its own — without this, the colors can
        // look correct in the Editor but silently fail to persist through a
        // save or a Play Mode session, reverting to the tiles' default color.
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(wallTilemap);
            EditorUtility.SetDirty(gateTilemap);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    /// <summary>
    /// Wipes the ground layer back to solid ground tile. Manual/explicit only
    /// — never called automatically (not from Start(), not from
    /// GenerateWallsAndGates()) because it destroys hand-painted ground.
    /// Use it once per new level to get a blank canvas to paint over, or if
    /// you genuinely want to reset a level's ground back to flat grass.
    /// </summary>
    [ContextMenu("Fill Ground (Reset)")]
    public void FillGround()
    {
        if (groundTilemap == null)
        {
            Debug.LogError("CastleMapGenerator: assign the Ground Tilemap in the Inspector first.");
            return;
        }

        groundTilemap.ClearAllTiles();

        for (int col = 0; col < GridMath.Columns; col++)
            for (int row = 0; row < GridMath.Rows; row++)
                groundTilemap.SetTile(GridMath.TileToCell(new Vector2Int(col, row)), groundTile);

        Debug.Log($"Ground reset to solid {groundTile}: {GridMath.Columns}x{GridMath.Rows} tiles.");

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(groundTilemap);
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
        // Walls/gates regenerate at runtime, unconditionally: they're fully
        // described by the region lists above, so there's no need to depend
        // on whatever got painted and saved in the Editor beforehand — every
        // Play session repaints itself fresh.
        //
        // Ground is deliberately excluded: it's hand-painted per level, so
        // Start() must never call FillGround() or otherwise touch
        // groundTilemap — doing so would wipe hand-painted work every time
        // you press Play.
        GenerateWallsAndGates();
    }
}
