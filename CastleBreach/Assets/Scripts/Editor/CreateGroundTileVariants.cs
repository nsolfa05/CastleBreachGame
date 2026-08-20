using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tools > Castle Breach > Create Ground Tile Variants. Same problem
/// <see cref="CreatePlaceholderTiles"/> solved for the three original
/// placeholder tiles, generalized: turns any sprite you drag in (e.g. a
/// dirt or flower cell sliced out of GentleForestV01.png) into a proper
/// Tile asset in Assets/Tiles, ready to drop into a Tile Palette. Sidesteps
/// the same two traps as before — newer Unity's missing plain "Create >
/// Tile" menu entry, and dragging a sprite into the Hierarchy first
/// silently creating a Prefab instead of a Tile.
///
/// Deliberately doesn't guess sprite names off the sheet — slicing can name
/// sub-sprites differently across Unity versions, so you pick each sprite
/// visually (same as Guide 12a's grid reference image), same as any normal
/// Tile Palette workflow.
/// </summary>
public class CreateGroundTileVariants : EditorWindow
{
    private class Settings : ScriptableObject
    {
        public Sprite[] sprites = new Sprite[0];
    }

    private const string TilesFolder = "Assets/Tiles";

    private Settings settings;
    private SerializedObject serializedSettings;
    private SerializedProperty spritesProp;

    [MenuItem("Tools/Castle Breach/Create Ground Tile Variants")]
    public static void ShowWindow()
    {
        GetWindow<CreateGroundTileVariants>("Ground Tile Variants");
    }

    private void OnEnable()
    {
        settings = ScriptableObject.CreateInstance<Settings>();
        serializedSettings = new SerializedObject(settings);
        spritesProp = serializedSettings.FindProperty("sprites");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Drag one or more sliced sprites here (e.g. dirt, flowers, or an " +
            "alternate grass cell from GentleForestV01.png in the Project " +
            "window), then click Create. One Tile asset gets created per " +
            "sprite in Assets/Tiles, named after the sprite — rename it " +
            "(F2) to something readable afterward, e.g. GroundTile_Dirt.",
            MessageType.Info);

        serializedSettings.Update();
        EditorGUILayout.PropertyField(spritesProp, true);
        serializedSettings.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("Create Tile Assets"))
            Create();
    }

    private void Create()
    {
        if (!AssetDatabase.IsValidFolder(TilesFolder))
            AssetDatabase.CreateFolder("Assets", "Tiles");

        int created = 0, skipped = 0;
        foreach (var sprite in settings.sprites)
        {
            if (sprite == null)
                continue;

            string path = $"{TilesFolder}/{sprite.name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Tile>(path) != null)
            {
                Debug.Log($"CreateGroundTileVariants: {path} already exists — leaving it alone.");
                skipped++;
                continue;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            // White, not baked color: real art carries its own color (Guide 12a),
            // unlike the three original tint-a-white-square placeholders.
            tile.color = Color.white;
            AssetDatabase.CreateAsset(tile, path);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"CreateGroundTileVariants: created {created} tile asset(s) in {TilesFolder} " +
                  $"({skipped} already existed).");
    }
}
