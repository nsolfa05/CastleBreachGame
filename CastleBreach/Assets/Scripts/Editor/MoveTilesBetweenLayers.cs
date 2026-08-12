using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tools > Castle Breach > Move Tiles Between Layers. Recovery tool for the
/// "painted on the wrong Tilemap" mistake — moves every tile from a source
/// Tilemap into a target Tilemap, cell by cell, then clears it from the
/// source. "Tile To Preserve" (optional) is left alone in the source and
/// never copied — set it to WallTile/GateTile so tiles accidentally painted
/// onto Walls/Gates move to Ground without disturbing the actual border
/// walls/gates sitting in the same Tilemap.
///
/// After moving, press Play (or run CastleMapGenerator's "Generate Walls &&
/// Gates") once — that fully rebuilds Walls/Gates from the region data
/// regardless of prior state, so any real wall/gate cell that got
/// overwritten by the mistake in the first place is restored for free.
/// </summary>
public class MoveTilesBetweenLayers : EditorWindow
{
    private Tilemap source;
    private Tilemap target;
    private TileBase preserveTile;

    [MenuItem("Tools/Castle Breach/Move Tiles Between Layers")]
    public static void ShowWindow()
    {
        GetWindow<MoveTilesBetweenLayers>("Move Tiles Between Layers");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Moves every tile from Source into Target, cell by cell, then " +
            "clears it from Source. Cells holding 'Tile To Preserve' (if " +
            "set) are left alone in Source and NOT moved — set this to " +
            "WallTile or GateTile when recovering ground tiles accidentally " +
            "painted onto Walls/Gates.",
            MessageType.Info);

        source = (Tilemap)EditorGUILayout.ObjectField("Source Tilemap", source, typeof(Tilemap), true);
        target = (Tilemap)EditorGUILayout.ObjectField("Target Tilemap", target, typeof(Tilemap), true);
        preserveTile = (TileBase)EditorGUILayout.ObjectField(
            "Tile To Preserve (optional)", preserveTile, typeof(TileBase), false);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(source == null || target == null || source == target))
        {
            if (GUILayout.Button("Move Tiles"))
                Move();
        }
    }

    private void Move()
    {
        source.CompressBounds();
        int moved = 0;

        foreach (var cell in source.cellBounds.allPositionsWithin)
        {
            var tile = source.GetTile(cell);
            if (tile == null || tile == preserveTile)
                continue;

            target.SetTile(cell, tile);
            source.SetTile(cell, null);
            moved++;
        }

        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(target);
        EditorSceneManager.MarkSceneDirty(source.gameObject.scene);

        Debug.Log($"MoveTilesBetweenLayers: moved {moved} tile(s) from {source.name} to {target.name}. " +
                  "Now press Play (or run Generate Walls && Gates) once to rebuild the source layer cleanly.");
    }
}
