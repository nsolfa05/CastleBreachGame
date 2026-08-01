using UnityEditor;

/// <summary>
/// Custom inspector for PlayerAttack — groups its fields (Combat, Visuals, and
/// whatever Guide 11b's weapon rework adds) into collapsible foldout sections
/// (see FoldoutHeaderEditor). Editor-only; never included in a build.
/// </summary>
[CustomEditor(typeof(PlayerAttack))]
public class PlayerAttackEditor : FoldoutHeaderEditor
{
}
