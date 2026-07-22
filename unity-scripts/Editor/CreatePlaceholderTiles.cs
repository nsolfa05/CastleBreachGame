using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// One-click generator for the vertical slice's placeholder Tile assets —
/// Tools > Castle Breach > Create Placeholder Tiles. Sidesteps the Tile
/// Palette drag-and-drop workflow, which is easy to trip up (dragging a
/// Sprite into the Hierarchy first creates a Prefab, not a Tile, and the
/// Prefab silently fails to show up in any TileBase field/picker).
/// </summary>
public static class CreatePlaceholderTiles
{
    private const string TilesFolder = "Assets/Tiles";

    [MenuItem("Tools/Castle Breach/Create Placeholder Tiles")]
    public static void Create()
    {
        var guids = AssetDatabase.FindAssets("Square t:Sprite");
        if (guids.Length == 0)
        {
            Debug.LogError("CreatePlaceholderTiles: no Sprite named 'Square' found in the " +
                            "project. Create it first (Assets/Sprites, right-click > Create > " +
                            "2D > Sprites > Square).");
            return;
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));

        if (!AssetDatabase.IsValidFolder(TilesFolder))
            AssetDatabase.CreateFolder("Assets", "Tiles");

        CreateIfMissing("GroundTile", sprite);
        CreateIfMissing("WallTile", sprite);
        CreateIfMissing("GateTile", sprite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created GroundTile, WallTile and GateTile in Assets/Tiles " +
                   "(existing correct ones were left alone).");
    }

    private static void CreateIfMissing(string name, Sprite sprite)
    {
        string path = $"{TilesFolder}/{name}.asset";

        if (AssetDatabase.LoadAssetAtPath<Tile>(path) != null)
        {
            Debug.Log($"{name} already exists as a proper Tile asset — leaving it alone.");
            return;
        }

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.color = Color.white;
        AssetDatabase.CreateAsset(tile, path);
    }
}
