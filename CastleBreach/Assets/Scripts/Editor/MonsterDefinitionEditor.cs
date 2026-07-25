using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for MonsterDefinition — draws an estimated damage-per-second
/// summary box at the TOP of the asset's inspector (for the designer's
/// convenience), then the normal fields below. DPS = damage ÷ the full attack
/// cycle length (attack interval, plus the wind-up for telegraphed attackers).
/// Editor-only; never included in a build.
/// </summary>
[CustomEditor(typeof(MonsterDefinition))]
public class MonsterDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var def = (MonsterDefinition)target;

        float cycle = def.attackInterval;
        if (def.usesTelegraphedAreaAttack) cycle += def.telegraphTime; // wind-up counts against throughput
        cycle = Mathf.Max(0.0001f, cycle);

        float kingDamage = def.useUniqueKingDamage ? def.kingDamage : def.playerDamage;
        float praiseDamage = def.praiseTowerDamage > 0f ? def.praiseTowerDamage : def.structureDamage;

        string summary =
            $"Estimated DPS  (damage ÷ {cycle:0.##}s cycle" +
            (def.usesTelegraphedAreaAttack ? ", incl. telegraph wind-up)" : ")") + "\n" +
            $"    vs Player:        {def.playerDamage / cycle:0.#} / s\n" +
            $"    vs King:          {kingDamage / cycle:0.#} / s\n" +
            $"    vs Structure:     {def.structureDamage / cycle:0.#} / s\n" +
            $"    vs Praise Tower:  {praiseDamage / cycle:0.#} / s";

        EditorGUILayout.HelpBox(summary, MessageType.Info);
        EditorGUILayout.Space();

        DrawDefaultInspector();
    }
}
