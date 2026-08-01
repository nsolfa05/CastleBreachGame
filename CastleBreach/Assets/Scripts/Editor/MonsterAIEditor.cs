using UnityEditor;

/// <summary>
/// Custom inspector for MonsterAI — groups its (many, and still growing)
/// tunables into collapsible foldout sections instead of one long flat list
/// (see FoldoutHeaderEditor). No preamble needed here; MonsterDefinition's own
/// inspector already carries the per-monster-type DPS summary.
/// Editor-only; never included in a build.
/// </summary>
[CustomEditor(typeof(MonsterAI))]
public class MonsterAIEditor : FoldoutHeaderEditor
{
}
