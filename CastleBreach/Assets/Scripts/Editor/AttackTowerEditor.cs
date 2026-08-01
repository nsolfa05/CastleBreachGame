using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for AttackTower — draws an estimated damage-per-second
/// summary box at the TOP of the component's inspector (for the designer's
/// convenience), then every field grouped into collapsible foldout sections
/// (see FoldoutHeaderEditor). DPS = Damage ÷ Seconds Between Shots, per target
/// hit — Splash Radius doesn't raise this number, it just means the same
/// damage also lands on everything else caught in the blast, not extra damage
/// on any single target. Editor-only; never included in a build. Mirrors
/// MonsterDefinitionEditor's DPS box, but reads via SerializedProperty since
/// AttackTower's stat fields are private.
/// </summary>
[CustomEditor(typeof(AttackTower))]
public class AttackTowerEditor : FoldoutHeaderEditor
{
    protected override void DrawPreamble()
    {
        float damage = serializedObject.FindProperty("damage").floatValue;
        float secondsBetweenShots = serializedObject.FindProperty("secondsBetweenShots").floatValue;
        float splashRadius = serializedObject.FindProperty("splashRadius").floatValue;
        bool hasProjectile = serializedObject.FindProperty("projectilePrefab").objectReferenceValue != null;

        float cycle = Mathf.Max(0.0001f, secondsBetweenShots);
        float dps = damage / cycle;

        string summary = $"Estimated DPS (per target hit): {dps:0.#} / s" +
            $"\n    Damage {damage:0.#} ÷ {cycle:0.##}s cycle";
        if (splashRadius > 0f)
            summary += $"\nSplash Radius {splashRadius:0.#}: the SAME damage hits everything caught in that radius — more targets, not more damage per target.";
        if (!hasProjectile)
            summary += "\n(No Projectile Prefab — instant melee hit; same DPS cycle still applies.)";

        EditorGUILayout.HelpBox(summary, MessageType.Info);
        EditorGUILayout.Space();
    }
}
