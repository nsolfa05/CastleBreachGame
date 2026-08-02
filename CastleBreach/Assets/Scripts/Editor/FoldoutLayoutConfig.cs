using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[Serializable]
public class FoldoutSectionOverride
{
    /// <summary>The [Header] text from code — stable identity even after renaming (matching, and each field's own foldout expand/collapse state, is keyed on this, never on displayName).</summary>
    public string originalName;

    /// <summary>What's actually shown. Falls back to originalName when empty.</summary>
    public string displayName;
}

[Serializable]
public class FoldoutTypeLayout
{
    /// <summary>target.GetType().FullName — one shared layout per SCRIPT, not per asset, so every Monster Definition (Cyclops, Zombie, ...) shows the same organization.</summary>
    public string typeName;

    /// <summary>Display order IS list order.</summary>
    public List<FoldoutSectionOverride> sections = new List<FoldoutSectionOverride>();
}

/// <summary>
/// Stores the user's own renaming/reordering of FoldoutHeaderEditor sections,
/// per target type. One shared, git-tracked asset (auto-created on first use,
/// same convention as everything else in this codebase that builds itself at
/// runtime) rather than EditorPrefs — EditorPrefs is per-MACHINE, not
/// per-project, so a layout customization made here needs to live in the repo
/// to travel with it instead of silently not existing on a different machine.
/// </summary>
public class FoldoutLayoutConfig : ScriptableObject
{
    private const string AssetPath = "Assets/Scripts/Editor/FoldoutLayoutConfig.asset";

    public List<FoldoutTypeLayout> types = new List<FoldoutTypeLayout>();

    private static FoldoutLayoutConfig instance;

    public static FoldoutLayoutConfig Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = AssetDatabase.LoadAssetAtPath<FoldoutLayoutConfig>(AssetPath);
            if (instance == null)
            {
                instance = CreateInstance<FoldoutLayoutConfig>();
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                AssetDatabase.CreateAsset(instance, AssetPath);
                AssetDatabase.SaveAssets();
            }
            return instance;
        }
    }

    public FoldoutTypeLayout GetOrCreateLayout(string typeName)
    {
        var layout = types.Find(t => t.typeName == typeName);
        if (layout == null)
        {
            layout = new FoldoutTypeLayout { typeName = typeName };
            types.Add(layout);
        }
        return layout;
    }

    public void Save()
    {
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }
}
