using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Base class for a custom Inspector that groups every serialized field into
/// COLLAPSIBLE foldout sections, one per [Header("...")] found on the fields
/// themselves — instead of Unity's default flat list, where a Header is just a
/// bold label with no way to fold it away. Nothing about section layout needs
/// maintaining here: add a new [Header] (or another field under an existing
/// one) to the target script and it becomes part of the right foldout
/// automatically. A field with no preceding Header falls into a generic
/// "Other" section rather than silently vanishing.
///
/// Foldout open/closed state persists per-object via SessionState (survives
/// reselecting it, or other Editor churn, within the same Unity session;
/// resets when Unity restarts) — plain fields on the Editor instance would
/// reset every time the target is deselected/reselected, which defeats the
/// point of keeping sections organized while you work.
///
/// Subclasses override DrawPreamble() for anything that belongs ABOVE the
/// foldouts (e.g. a DPS summary box); most inspectors won't need to.
/// Editor-only; never included in a build.
/// </summary>
public abstract class FoldoutHeaderEditor : Editor
{
    private const string OtherSectionName = "Other";

    /// <summary>Override to draw something above the foldout sections (a summary box, etc). Default: nothing.</summary>
    protected virtual void DrawPreamble() { }

    public override void OnInspectorGUI()
    {
        // Synced BEFORE DrawPreamble, not just inside DrawFoldoutSections below —
        // a subclass's preamble (e.g. AttackTowerEditor's DPS box) reads current
        // values via serializedObject.FindProperty and needs them up to date too.
        serializedObject.Update();
        DrawPreamble();
        DrawFoldoutSections();
    }

    private void DrawFoldoutSections()
    {
        var type = target.GetType();

        string openSection = null;
        bool openExpanded = true;

        var iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            // The auto-generated "Script" reference — show once, plain, up top,
            // disabled (standard Unity convention), outside any foldout.
            if (iterator.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(iterator);
                continue;
            }

            string ownHeader = GetHeaderFor(type, iterator.name);
            string sectionName = ownHeader ?? openSection ?? OtherSectionName;

            if (sectionName != openSection)
            {
                if (openSection != null) EditorGUILayout.EndFoldoutHeaderGroup();
                openSection = sectionName;
                EditorGUILayout.Space(4);
                openExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(GetFoldoutState(openSection), openSection);
                SetFoldoutState(openSection, openExpanded);
            }

            if (openExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(iterator, true);
                EditorGUI.indentLevel--;
            }
        }

        if (openSection != null) EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>The [Header("...")] text on this field, or null if it has none
    /// (i.e. it continues whatever section came before it).</summary>
    private static string GetHeaderFor(System.Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null) return null;
        var header = field.GetCustomAttribute<HeaderAttribute>();
        return header?.header;
    }

    private bool GetFoldoutState(string section) =>
        SessionState.GetBool(FoldoutKey(section), true); // default expanded

    private void SetFoldoutState(string section, bool value) =>
        SessionState.SetBool(FoldoutKey(section), value);

    /// <summary>
    /// A key stable across reselecting the SAME object — required for the
    /// "stays collapsed while you keep working" guarantee, since Unity creates
    /// a brand-new Editor instance every time an object is (re)selected, so
    /// anything assigned fresh per-Editor-instance (e.g. a random value handed
    /// out once) would reset on every reselection and defeat the point of using
    /// SessionState at all. Deliberately NOT Object.GetInstanceID()/GetEntityId()
    /// — that class of API keeps changing across Unity versions (see the
    /// standDownKey precedent in MonsterAI) — so this uses the target's asset
    /// path when it has one (ScriptableObjects, prefab assets: unique and
    /// permanent unless the file moves) and falls back to type+name for a
    /// scene-instance component (unique enough in practice; a rare name
    /// collision just means two objects cosmetically share fold state, which is
    /// harmless UI-only convenience data, not anything that affects gameplay).
    /// </summary>
    private string FoldoutKey(string section)
    {
        string path = AssetDatabase.GetAssetPath(target);
        string identity = !string.IsNullOrEmpty(path) ? path : $"{target.GetType().Name}/{target.name}";
        return $"{GetType().Name}.{identity}.{section}";
    }
}
