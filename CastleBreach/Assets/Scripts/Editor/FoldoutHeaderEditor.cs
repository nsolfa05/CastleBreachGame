using System.Collections.Generic;
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
/// "Other" section rather than silently vanishing. A field can also override
/// its own displayed label via [InspectorLabel("...")], independent of its
/// C# name.
///
/// Foldout open/closed state persists per-object via SessionState (survives
/// reselecting it, or other Editor churn, within the same Unity session;
/// resets when Unity restarts) — plain fields on the Editor instance would
/// reset every time the target is deselected/reselected, which defeats the
/// point of keeping sections organized while you work.
///
/// SECTION LAYOUT (title + order) is further customizable BY HAND, without
/// touching code: toggle "Edit Section Layout" at the top of the Inspector to
/// reveal a rename field + reorder arrows under each section. This is stored
/// per TARGET TYPE (so every Monster Definition asset shares one layout, not
/// one each) in FoldoutLayoutConfig, a small git-tracked asset — see that
/// class. Renaming only changes the DISPLAY text; grouping and expand/collapse
/// state still key off the original code-side [Header] text, so a later code
/// change (a new field added under an existing header) still lands in the
/// right — possibly renamed, possibly reordered — section automatically.
///
/// Subclasses override DrawPreamble() for anything that belongs ABOVE the
/// foldouts (e.g. a DPS summary box); most inspectors won't need to.
/// Editor-only; never included in a build.
/// </summary>
public abstract class FoldoutHeaderEditor : Editor
{
    private const string OtherSectionName = "Other";

    private class SectionGroup
    {
        public string key; // canonical [Header] text — stable identity
        public readonly List<string> propertyPaths = new List<string>();
    }

    /// <summary>Override to draw something above the foldout sections (a summary box, etc). Default: nothing.</summary>
    protected virtual void DrawPreamble() { }

    public override void OnInspectorGUI()
    {
        // Synced BEFORE DrawPreamble, not just inside DrawFoldoutSections below —
        // a subclass's preamble (e.g. AttackTowerEditor's DPS box) reads current
        // values via serializedObject.FindProperty and needs them up to date too.
        serializedObject.Update();
        DrawPreamble();
        DrawLayoutToolbar();
        DrawFoldoutSections();
    }

    private bool EditingLayout
    {
        get => SessionState.GetBool(EditLayoutKey(), false);
        set => SessionState.SetBool(EditLayoutKey(), value);
    }

    // Keyed by TARGET TYPE, not by object — matches FoldoutLayoutConfig
    // sharing one layout across every asset of the same type, so toggling
    // "Edit Section Layout" while looking at the Cyclops stays on when you
    // click over to the Zombie right after.
    private string EditLayoutKey() => $"{GetType().Name}.EditLayout.{target.GetType().FullName}";

    private void DrawLayoutToolbar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            bool editing = EditingLayout;
            bool newEditing = GUILayout.Toggle(editing, "Edit Section Layout", EditorStyles.miniButton, GUILayout.Width(140));
            if (newEditing != editing) EditingLayout = newEditing;

            if (newEditing && GUILayout.Button("Reset Layout", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                var config = FoldoutLayoutConfig.Instance;
                config.types.RemoveAll(t => t.typeName == target.GetType().FullName);
                config.Save();
            }
        }
        EditorGUILayout.Space(2);
    }

    private void DrawFoldoutSections()
    {
        var type = target.GetType();

        // Pass 1 — group every visible property (after m_Script) by its own
        // [Header], or whatever section is still open if it has none. Deferred
        // (not drawn yet) so display ORDER can come from FoldoutLayoutConfig
        // instead of declaration order.
        var groups = new List<SectionGroup>(); // in first-encountered (declaration) order
        var groupByKey = new Dictionary<string, SectionGroup>();
        string currentKey = null;

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
            string key = ownHeader ?? currentKey ?? OtherSectionName;
            currentKey = key;

            if (!groupByKey.TryGetValue(key, out var group))
            {
                group = new SectionGroup { key = key };
                groupByKey[key] = group;
                groups.Add(group);
            }
            group.propertyPaths.Add(iterator.propertyPath);
        }

        // Pass 2 — resolve display order/names from the persisted layout,
        // self-healing: any section not seen before (a brand-new [Header], or
        // the very first time this type is ever inspected) gets appended at
        // the end in natural order and remembered from now on.
        var config = FoldoutLayoutConfig.Instance;
        var layout = config.GetOrCreateLayout(type.FullName);
        bool layoutChanged = false;
        foreach (var group in groups)
        {
            if (layout.sections.Exists(s => s.originalName == group.key)) continue;
            layout.sections.Add(new FoldoutSectionOverride { originalName = group.key, displayName = group.key });
            layoutChanged = true;
        }

        // Pass 3 — draw, in layout.sections order. Reorder clicks are deferred
        // to AFTER this loop (applied once, below) rather than mutating
        // layout.sections while it's being enumerated — foreach over a list
        // being modified mid-iteration throws.
        bool editingLayout = EditingLayout;
        FoldoutSectionOverride pendingMoveEntry = null;
        int pendingMoveDirection = 0;
        bool renameChanged = false;

        foreach (var entry in layout.sections.ToArray())
        {
            if (!groupByKey.TryGetValue(entry.originalName, out var group)) continue;
            DrawSection(type, entry, group, layout, editingLayout, ref pendingMoveEntry, ref pendingMoveDirection, ref renameChanged);
        }

        if (pendingMoveEntry != null)
        {
            MoveSection(layout, pendingMoveEntry, pendingMoveDirection);
            config.Save();
        }
        else if (renameChanged || layoutChanged)
        {
            config.Save();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSection(System.Type type, FoldoutSectionOverride entry, SectionGroup group, FoldoutTypeLayout layout,
        bool editingLayout, ref FoldoutSectionOverride pendingMoveEntry, ref int pendingMoveDirection, ref bool renameChanged)
    {
        string displayName = string.IsNullOrEmpty(entry.displayName) ? entry.originalName : entry.displayName;

        EditorGUILayout.Space(4);
        bool expanded = EditorGUILayout.Foldout(GetFoldoutState(entry.originalName), displayName, true, EditorStyles.foldoutHeader);
        SetFoldoutState(entry.originalName, expanded);

        if (editingLayout)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(14);
                string newName = EditorGUILayout.TextField(displayName);
                if (newName != displayName) { entry.displayName = newName; renameChanged = true; }

                using (new EditorGUI.DisabledScope(!CanMove(layout, entry, -1)))
                    if (GUILayout.Button("▲", GUILayout.Width(22))) { pendingMoveEntry = entry; pendingMoveDirection = -1; }
                using (new EditorGUI.DisabledScope(!CanMove(layout, entry, 1)))
                    if (GUILayout.Button("▼", GUILayout.Width(22))) { pendingMoveEntry = entry; pendingMoveDirection = 1; }
            }
        }

        if (!expanded) return;

        EditorGUI.indentLevel++;
        foreach (var path in group.propertyPaths)
        {
            var property = serializedObject.FindProperty(path);
            if (property == null) continue; // shouldn't happen, but a stale path should never hard-fail the Inspector

            string customLabel = GetLabelFor(type, property.name);
            if (customLabel != null)
                EditorGUILayout.PropertyField(property, new GUIContent(customLabel, property.tooltip), true);
            else
                EditorGUILayout.PropertyField(property, true);
        }
        EditorGUI.indentLevel--;
    }

    private static bool CanMove(FoldoutTypeLayout layout, FoldoutSectionOverride entry, int direction)
    {
        int index = layout.sections.IndexOf(entry);
        int newIndex = index + direction;
        return newIndex >= 0 && newIndex < layout.sections.Count;
    }

    private static void MoveSection(FoldoutTypeLayout layout, FoldoutSectionOverride entry, int direction)
    {
        int index = layout.sections.IndexOf(entry);
        int newIndex = index + direction;
        if (newIndex < 0 || newIndex >= layout.sections.Count) return;
        layout.sections.RemoveAt(index);
        layout.sections.Insert(newIndex, entry);
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

    /// <summary>The [InspectorLabel("...")] override on this field, or null to use Unity's default nicified name.</summary>
    private static string GetLabelFor(System.Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var label = field?.GetCustomAttribute<InspectorLabelAttribute>();
        return label?.Label;
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
    /// path when it has one (ScriptableObject/prefab assets: unique and
    /// permanent unless the file moves) and falls back to type+name for a
    /// scene-instance component (unique enough in practice; a rare name
    /// collision just means two objects cosmetically share fold state, which is
    /// harmless UI-only convenience data, not anything that affects gameplay).
    /// section is always the ORIGINAL [Header] text, never the user-renamed
    /// display text — so renaming a section never resets its expand state.
    /// </summary>
    private string FoldoutKey(string section)
    {
        string path = AssetDatabase.GetAssetPath(target);
        string identity = !string.IsNullOrEmpty(path) ? path : $"{target.GetType().Name}/{target.name}";
        return $"{GetType().Name}.{identity}.{section}";
    }
}
